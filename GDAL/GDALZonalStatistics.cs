using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NLog;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;

namespace GDAL
{
    /// <summary>
    /// 
    /// </summary>
    class GdalZonalStatistics
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // https://gis.stackexchange.com/questions/208441/zonal-statistics-of-a-polygon-and-assigning-mean-value-to-the-polygon
        // https://gist.github.com/perrygeo/5667173
        // https://towardsdatascience.com/zonal-statistics-algorithm-with-python-in-4-steps-382a3b66648a
        // https://www.gisremotesensing.com/2015/09/clip-raster-with-shapefile-using-c-and.html
        // https://gis.stackexchange.com/questions/243150/reversing-orientation-of-polygon-rings-in-gdal-c

        //ComputeZoneStatistics and Paralelize

        public static void ComputeZonalStatistics(string InputPolygonFile, string InputRasterFile, string OutputRasterFile) {

            // Open vector data and buffer each Geometry to fix possible geometry errors.
            var VectorDataSource = Ogr.Open(InputPolygonFile, 0);
            var layer = VectorDataSource.GetLayerByIndex(0);

            var ftrs = new List<Feature>();
            var feature = layer.GetNextFeature();
            while (feature != null) {
                ftrs.Add(feature);
                feature = layer.GetNextFeature();
            }

            // Create in MEM raster for each geometry
            foreach(var ftr in ftrs) {
                var env = new Envelope();
                ftr.GetGeometryRef().GetEnvelope(env);

                // En el lado menor: al menos 100 celdas
                const double MIN_CELDAS = 100;
                var CellSize = Math.Min((env.MaxX - env.MinX) / MIN_CELDAS, (env.MaxY - env.MinY) / MIN_CELDAS); 

                int x_res = Convert.ToInt32((env.MaxX - env.MinX) / CellSize);  
                int y_res = Convert.ToInt32((env.MaxY - env.MinY) / CellSize);



            }

        }
        public static void CLipRasterWithVector(string InputPolygonFile, string InputRasterFile, string OutputRasterFile) {

            var rasterCellSize = 1000;
            var noDataValue = -9999;
            var fieldName = "fid";

            //Reading the vector data  
            var dataSource = Ogr.Open(InputPolygonFile, 0);
            var layer = dataSource.GetLayerByIndex(0);

            var envelope = new Envelope();  
            layer.GetExtent(envelope, 0);  
            //Compute the out raster cell resolutions  
            int x_res = Convert.ToInt32((envelope.MaxX - envelope.MinX) / rasterCellSize);  
            int y_res = Convert.ToInt32((envelope.MaxY - envelope.MinY) / rasterCellSize);  
            Console.WriteLine("Extent: " + envelope.MaxX + " " + envelope.MinX + " " + envelope.MaxY + " " + envelope.MinY);  
            Console.WriteLine("X resolution: " + x_res);  
            Console.WriteLine("X resolution: " + y_res);

            //Check if output raster exists & delete (optional)  
            if (File.Exists(OutputRasterFile)) File.Delete(OutputRasterFile);

            //Create new tiff   
            var outputDriver = Gdal.GetDriverByName("GTiff");  
            var outputDataset = outputDriver.Create(OutputRasterFile, x_res, y_res, 1, DataType.GDT_Float64, null);

            // Extract srs from input feature and Assign to outpur raster  
            string inputShapeSrs;  
            SpatialReference spatialRefrence = layer.GetSpatialRef();  
            spatialRefrence.ExportToWkt(out inputShapeSrs);  
            outputDataset.SetProjection(inputShapeSrs);  

            //Set Geotransform  
            var argin = new double[] { envelope.MinX, rasterCellSize, 0, envelope.MaxY, 0, -rasterCellSize };  
            outputDataset.SetGeoTransform(argin);  
            //Set no data  
            Band band = outputDataset.GetRasterBand(1);  
            band.SetNoDataValue(noDataValue);  
            //close tiff  
            outputDataset.FlushCache();  
            outputDataset.Dispose();  

            //Feature to raster rasterize layer options  
            //No of bands (1)  
            int[] bandlist = new int[] { 1 };  
            //Values to be burn on raster (10.0)  
            double[] burnValues = new double[] { 10.0 };  
            Dataset myDataset = Gdal.Open(OutputRasterFile, Access.GA_Update);  
            //additional options  
            string[] rasterizeOptions;  
            //rasterizeOptions = new string[] { "ALL_TOUCHED=TRUE", "ATTRIBUTE=" + fieldName }; //To set all touched pixels into raster pixel  
            rasterizeOptions = new string[] { "ATTRIBUTE=" + fieldName };  
           //Rasterize layer  
           //Gdal.RasterizeLayer(myDataset, 1, bandlist, layer, IntPtr.Zero, IntPtr.Zero, 1, burnValues, null, null, null); // To burn the given burn values instead of feature attributes  
             Gdal.RasterizeLayer(myDataset, 1, bandlist, layer, IntPtr.Zero, IntPtr.Zero, 1, burnValues, rasterizeOptions, GdalUtils.GDalProgress, "Raster conversion");  
        }

        public static IEnumerable<Geometry> BufferPolygons(IEnumerable<Geometry> polygons, double BufferDistance) {

            var ret = new List<Geometry>();
            var locker = new object();
            Parallel.ForEach(polygons, polygon =>
            {
                var polygonBuffer = polygon.Buffer(BufferDistance, 1);
                lock (locker) ret.Add(polygonBuffer);
            });

            return ret; 
        }
    }
}
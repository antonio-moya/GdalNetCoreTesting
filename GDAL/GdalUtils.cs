using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MaxRev.Gdal.Core;
using Newtonsoft.Json;
using NLog;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;

namespace GDAL
{
    /// <summary>
    /// Todo esto en: https://github.com/bertt/GdalOnNetCoreSample
    /// </summary>
    class GdalUtils
    {

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        public static void GDalErrorHandler (int eclass, int code, IntPtr msg)
        {
            var message = msg != IntPtr.Zero ? Marshal.PtrToStringAnsi(msg) : string.Empty;
            logger.Error($"==>=>=>=> Error GDAL: {message}");
        }

        public static int GDalProgress(double complete, System.IntPtr msg, System.IntPtr data) {
            var message = msg != IntPtr.Zero ? Marshal.PtrToStringAnsi(msg) : string.Empty;
            var datos = data != IntPtr.Zero ? Marshal.PtrToStringAnsi(data) : string.Empty;
            logger.Info($"P:{(complete*100).ToString("0.0")}% {message} {data} ");
            //logger.Trace($" {datos} ");
            return 1;
        }

        /// <summary>
        /// Configure and initialize GDAL libraries
        /// </summary>
        public static void Configure() => GdalBase.ConfigureAll();

        public static void GetGdalInfo()
        {            
            logger.Trace(Gdal.VersionInfo("RELEASE_NAME"));
            logger.Trace(Gdal.VersionInfo("VERSION_NUM"));
            logger.Trace(Gdal.VersionInfo("BUILD_INFO"));

            var drivers = new List<string>();
            for (var i = 0; i < Ogr.GetDriverCount(); i++) drivers.Add(Ogr.GetDriver(i).GetName());
            logger.Trace($"OGR Drivers list: {string.Join(",", drivers)}.");            
            drivers = new List<string>();
            for (var i = 0; i < Gdal.GetDriverCount(); i++) drivers.Add(Gdal.GetDriver(i).GetDescription());
            logger.Trace($"GDAL Drivers list: {string.Join(",", drivers)}.");
        }

        /// <summary>
        /// Simple coordinate reprojection
        /// </summary>
        /// <param name="EpsgIn"></param>
        /// <param name="EpsgOut"></param>
        /// <returns></returns>
        public static double[] ReprojectCoordinates(int EpsgIn, int EpsgOut, double x, double y, double z) {

            var src = new OSGeo.OSR.SpatialReference(EPSG2WKT(EpsgIn));
            logger.Trace($"SOURCE IsGeographic:" + src.IsGeographic() + " IsProjected:" + src.IsProjected());
            var dst = new OSGeo.OSR.SpatialReference(EPSG2WKT(EpsgOut));
            logger.Trace("DEST IsGeographic:" + dst.IsGeographic() + " IsProjected:" + dst.IsProjected());
            var ct = new OSGeo.OSR.CoordinateTransformation(src, dst);
            double[] p = new double[] {x,y,z};
            logger.Trace("From: x:" + p[0] + " y:" + p[1] + " z:" + p[2]);
            ct.TransformPoint(p);
            logger.Trace("To: x:" + p[0] + " y:" + p[1] + " z:" + p[2]);
            return p;
        }

        /// <summary>
        /// Gest the Src WKT given the EPSG code
        /// </summary>
        /// <param name="EPSG"></param>
        public static string EPSG2WKT(int EPSG) {
            var src = new OSGeo.OSR.SpatialReference("");
            src.ImportFromEPSG(EPSG);
            src.ExportToWkt(out var proj_wkt, null);
            return proj_wkt;
        }

        /// <summary>
        /// Translation of raster format
        /// </summary>
        /// <param name="InputRaster"></param>
        /// <param name="OutputRaster"></param>
        /// <param name="OutputDriver">driver neme for the output file, from GDAL drivers list</param>
        public static void TranslateRasterFormat(string InputRaster, string OutputRaster, string OutputDriver) {
            try
            {
                var opts = new string[] { "-sds", "-of", OutputDriver };
                using(var ds = Gdal.Open( InputRaster, Access.GA_ReadOnly )) {
                    var options = new GDALTranslateOptions(opts);
                    using(var outputDS = Gdal.wrapper_GDALTranslate(OutputRaster, ds, options, (Gdal.GDALProgressFuncDelegate)GDalProgress, $"Translare {InputRaster} to {OutputRaster} ({OutputDriver})" )) {
                        outputDS.FlushCache();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Application error: " + e.Message);
            }
        }

        /// <summary>
        /// Reproject a raster.
        /// Working with errors
        /// </summary>
        /// <param name="InputFilePath"></param>
        /// <param name="OutputFilePath"></param>
        /// <param name="EPSG">output EPSG</param>
        public static void RasterReprojection(string InputFilePath, string OutputFilePath, int EPSG) 
        {
            // TODO: Check for errors!
            var sr = new OSGeo.OSR.SpatialReference(null);
            sr.ImportFromEPSG(EPSG);
            sr.ExportToWkt(out var srs_wkt, null);

            // reproject raster and save to EHdr
            using( var srcDs = Gdal.Open(InputFilePath, Access.GA_ReadOnly))
            using( var vrt = Gdal.AutoCreateWarpedVRT(srcDs, srcDs.GetProjectionRef(), srs_wkt, ResampleAlg.GRA_Average, 0)) {
                var OutDirver = srcDs.GetDriver();
                OutDirver.CreateCopy(OutputFilePath, vrt, 1, null, GDalProgress, "Raster Reprojection");
            }
            //using( var MemDirver = Gdal.GetDriverByName("MEM"))

            //using( var bilDirver = srcDs.GetDriver() Gdal.GetDriverByName("EHdr"))
            //using( var bilfile = bilDirver.CreateCopy("lo que sea/asd/", vrt, 1, null,null, null))
     
            return;
        }

        /// <summary>
        /// Build a contour vector layer
        /// </summary>
        /// <param name="InputRaster"></param>
        /// <param name="OutputShp">output shapefile</param>
        /// <param name="ContoutInterval">increment balue (from CotourBase) to create contour</param>
        /// <param name="ContourBase">initial value to compute contour</param>
        public static void Contour(string InputRaster, string OutputShp, double ContourInterval, double ContourBase) {

            // https://gis.stackexchange.com/questions/210500/calling-gdal-contour-from-python-ipython

            using(var dsc = Gdal.Open(InputRaster, Access.GA_ReadOnly)) {

                var band = dsc.GetRasterBand(1);
                
                // Generate layer to save Contourlines in
                using(var ogr_ds = Ogr.GetDriverByName("ESRI Shapefile").CreateDataSource(OutputShp,  new string[] { "srs", dsc.GetProjection() })) {

                    var srs = new OSGeo.OSR.SpatialReference(dsc.GetProjection());
                    var contour_shp = ogr_ds.CreateLayer("contour", srs, wkbGeometryType.wkbLineString, null);
                    contour_shp.CreateField(new FieldDefn("ID", FieldType.OFTInteger), 1);
                    contour_shp.CreateField(new FieldDefn("ELEVATION", FieldType.OFTReal), 2);

                    //Generate Contour lines
                    Gdal.ContourGenerate(band, ContourInterval, ContourBase, 0, null, 0, -9999.9, contour_shp, contour_shp.FindFieldIndex("ID",0), contour_shp.FindFieldIndex("ELEVATION",0), GDalProgress, string.Empty);
                }
            }

        }

        /// <summary>
        /// Creates a GeoTIFF file
        /// </summary>
        /// <param name="GDalOutputDriver">Output driver</param>
        /// <param name="OutputFile"></param>
        /// <param name="Rows"></param>
        /// <param name="Columns"></param>
        /// <param name="SrcWkt"></param>
        /// <param name="GeoTransform"></param>
        /// <param name="RasterMetadata"></param>
        /// <param name="BandMetadata"></param>
        public static void CreateRaster(string GDalOutputDriver, string OutputFile, int Rows, int Columns, double MinX, double MinY, double CellSize, string SrcWkt, double[] GeoTransform, IEnumerable<float[]> BandsValues, string RasterMetadata, IEnumerable<string> BandMetadata) {

            var NumBands = 1;
            if (BandsValues != null) NumBands = BandsValues.Count();
            
            int height = Rows;
            int width = Columns;
            int bXSize = width;
            int bYSize = 1;

            try
            {
                /* -------------------------------------------------------------------- */
                /*      Get drivers                                                      */
                /* -------------------------------------------------------------------- */
                var GeotiffDriver = Gdal.GetDriverByName(GDalOutputDriver);
                if (GeotiffDriver == null) {
                    throw new Exception($"{GDalOutputDriver} GDal driver no encontrado, ¿se ha llamado a la configuración de GDal?");
                }

                /* -------------------------------------------------------------------- */
                /*      Create geotiff dataset.                                         */
                /* -------------------------------------------------------------------- */
                string[] options = new string [] { $"BLOCKXSIZE={bXSize}", $"BLOCKYSIZE={bYSize}" };
                using(var ds = GeotiffDriver.Create(OutputFile, width, height, NumBands, DataType.GDT_Float32, options)) {
                    
                    // Set geo transform
                    // https://stackoverflow.com/questions/27166739/description-of-parameters-of-gdal-setgeotransform
                    ds.SetGeoTransform(GeoTransform);
                    
                    // Set coordinate system    
                    ds.SetProjection(SrcWkt);

                    // Set metadata
                    ds.SetMetadataItem("SUAT.INCLAM.NET", RasterMetadata, string.Empty);
                    
                    /* -------------------------------------------------------------------- */
                    /*      Setting corner GCPs.                                            */
                    /* -------------------------------------------------------------------- */
                    //GCP[] GCPs = new GCP[] { new GCP(44.5, 27.5, 0, 0, 0, "info0", "id0"), new GCP(45.5, 27.5, 0, 100, 0, "info1", "id1"), new GCP(44.5, 26.5, 0, 0, 100, "info2", "id2"), new GCP(45.5, 26.5, 0, 100, 100, "info3", "id3") };
                    //ds.SetGCPs(GCPs, "");

                    // Escribir bandas
                    int NumBand = 1;
                    foreach(var vals in BandsValues) {
                        var BandValues = vals;
                        if (vals.Length < (width*height)) {
                            logger.Error(new ArgumentOutOfRangeException("BandsValues",vals, $"La longitud de los datos de la banda {NumBand} es menor que el número de filas por columnas."));
                            BandValues = new float[width*height];
                        }
                        var band = ds.GetRasterBand(NumBand);
                        band.SetNoDataValue(-9999.0);
                        if (BandMetadata!=null) {
                            if (BandMetadata.ElementAt(NumBand-1) != null) {
                                band.SetMetadata(BandMetadata.ElementAt(NumBand-1), "SUAT.INCLAM.NET");
                            }
                        }
                        band.WriteRaster(0, 0, width, height, BandValues, width, height, 0, 0);
                        band.FlushCache();
                        NumBand++;
                    }
                    
                    // Persistir a disco
                    ds.FlushCache();
                }

                /* -------------------------------------------------------------------- */
                /*      Create MEM dataset to support AddBand not during creation.      */
                /* -------------------------------------------------------------------- */
                /*
                var MemDriver = Gdal.GetDriverByName("MEM");
                OSGeo.GDAL.Dataset dsMem = null;
                using(var dsGeoTiff = Gdal.Open(InputRaster, Access.GA_ReadOnly)) {
                    dsMem = Gdal.GetDriverByName("MEM").CreateCopy("", dsGeoTiff, 0, null, (Gdal.GDALProgressFuncDelegate)GDalProgress, "GTiff to MEM copy" );
                }
                for(int band=1; band <5; band++) {

                    if (band>1) dsMem.AddBand(DataType.GDT_CFloat32, null);

                    var ba = dsMem.GetRasterBand(band);
                    var buffer = new float [w * h];
                    for (int i = 0; i < w; i++)
                        for (int j = 0; j < h; j++)
                            buffer[i * w + j] = (float)(i * 256 / w) * band;
                    ba.WriteRaster(0, 0, w, h, buffer, w, h, 0, 0);
                    ba.FlushCache();
                }
                dsMem.FlushCache();
                
                // Copy to source GeoTiff
                File.Delete(InputRaster);
                var dsTiff = GeotiffDriver.CreateCopy(InputRaster, dsMem, 0, null, (Gdal.GDALProgressFuncDelegate)GDalProgress, "MEM to Gtiff copy" );

                dsTiff.Dispose();
                dsMem.Dispose();
                */
            }
            catch (Exception e)
            {
                logger.Error(e);
                Console.WriteLine($"Application error: {e.Message} {Gdal.GetLastErrorMsg()}");
            }

        }

        /// <summary>
        /// Sums all rasters. Rasters must have the same rows and columns number.
        /// Suma todos los rasters y todas las bandas existentes en cada uno de ellos.
        /// </summary>
        /// <param name="RasterFiles"></param>
        public static void SumRasters(IEnumerable<string> RasterFiles, string OutputFile) {
            
            float[] SumArrays(float[] arr1, float[] arr2, float NoDataValue) {
                var ret = new float[arr1.Length];
                Parallel.For(0, arr1.Length, j =>            
                {
                    float val1 = arr1[j]!=NoDataValue ? arr1[j] : 0;
                    float val2 = arr2[j]!=NoDataValue ? arr2[j] : 0;
                    ret[j] = val1 + val2;
                });
                return ret;
            }
            
            float[] sum = null;
            var width = int.MinValue;
            var height= int.MinValue;
            foreach(var file in RasterFiles) {
                using(var ds = Gdal.Open(file, Access.GA_ReadOnly)) {
                    
                    if (sum==null) {
                        width = ds.RasterXSize;
                        height= ds.RasterYSize;
                        sum = new float[width*height];
                    }

                    for (int i = 1; i <= ds.RasterCount; i++) {
                        var band = ds.GetRasterBand(i);
                        var vals = new float[width*height];
                        band.ReadRaster(0, 0, width, height, vals, width, height, 0, 0);
                        band.GetNoDataValue(out double NoDataDouble, out int hasval);
                        var NoData = Convert.ToSingle(hasval!=0 ? NoDataDouble : 0);
                        sum = SumArrays(sum, vals, (float)NoData);
                    }
                }
            }

            if (sum==null) throw new Exception("No input raster for SUM");
            // Create out raster (similar to que first one)
            if (File.Exists(OutputFile)) File.Delete(OutputFile);
            File.Copy(RasterFiles.First(), OutputFile);
            using(var ds = Gdal.Open(OutputFile, Access.GA_Update)) {
                var band = ds.GetRasterBand(1);
                band.WriteRaster(0, 0, width, height, sum, width, height, 0, 0);
                object metadata =  new { 
                    type = "AEMet_radar sum",
                    ogirin = RasterFiles,
                    creation_time_utc = DateTime.Now.ToUniversalTime().ToString("yyyyMMddHHmmss")
                };
                band.SetMetadata(JsonConvert.SerializeObject(metadata), "SUAT.INCLAM.NET");
            }
        }

        /// <summary>
        /// Sets the color table for a raster band.
        /// Only works in GTiff of <see cref="Byte"/> or <see cref="UInt16"/>
        /// </summary>
        /// <param name="band"></param>
        public static void SetColorTable(Band band) {
            
            var ct = new ColorTable(PaletteInterp.GPI_RGB);
            var colors = new Color[] {
                Color.FromArgb(150,163, 255, 115),
                Color.FromArgb(150,38, 115, 0),
                Color.FromArgb(150,76, 230, 0),
                Color.FromArgb(150,112, 168, 0),
                Color.FromArgb(150,0, 92, 255),
                Color.FromArgb(150,197, 0, 255),
                Color.FromArgb(150,255, 170, 0),
                Color.FromArgb(150,0, 255, 197),
                Color.FromArgb(150,255, 255, 255)
            };

            var i = 10;
            foreach(var c in colors) {
                var ce = new ColorEntry();
                ce.c4 = c.A;
                ce.c3 = c.B;
                ce.c2 = c.G;
                ce.c1 = c.R;
                ct.SetColorEntry(i, ce);
                i+= 10;
            }
            band.SetRasterColorTable(ct);
        }

        /// <summary>
        /// Return all the data contanined in a raster band.
        /// Results is ordered by rows
        /// </summary>
        /// <param name="InputRaster"></param>
        /// <param name="Band"></param>
        /// <returns></returns>
        public static float[][] GetBandDataFloat(string InputRaster, int Band = 1) {
            float[][] ret = null;
            using(var ds = Gdal.Open(InputRaster, Access.GA_ReadOnly)) {
                var band = ds.GetRasterBand(Band);
                int width = ds.RasterXSize;
                int height = ds.RasterYSize;
                ret = new float[height][];
                
                for(int row=0; row <height; row++)  
                {  
                    ret[row] = new float[width];
                    band.ReadRaster(0, row, width, 1, ret[row], width, 1, 0, 0);   
                }
            }
            return ret;
        }

        public static Int16[][] GetBandDataInt16(string InputRaster, int Band = 1) {
            short[][] ret = null;
            using(var ds = Gdal.Open(InputRaster, Access.GA_ReadOnly)) {
                var band = ds.GetRasterBand(Band);
                int width = ds.RasterXSize;
                int height = ds.RasterYSize;
                ret = new short[height][];
                
                for(int row=0; row <height; row++)  
                {  
                    ret[row] = new short[width];
                    band.ReadRaster(0, row, width, 1, ret[row], width, 1, 0, 0);   
                }
            }
            return ret;
        }

        /// <summary>
        /// Col-Row to geo position
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="x">column number</param>
        /// <param name="y">row number</param>
        /// <returns></returns>
        public static double[] GDALInfoGetPosition(Dataset ds, double x, double y)
        {
            double[] adfGeoTransform = new double[6];
            double	dfGeoX, dfGeoY;
            ds.GetGeoTransform(adfGeoTransform);

            dfGeoX = adfGeoTransform[0] + adfGeoTransform[1] * x + adfGeoTransform[2] * y;
            dfGeoY = adfGeoTransform[3] + adfGeoTransform[4] * x + adfGeoTransform[5] * y;
            return new double[] {dfGeoX,dfGeoY};
        }

        /// <summary>
        /// Coordinates (X,Y) to pixels (COLUMN, ROW)
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        public static int[] GDALInfoGetRowCol(Dataset ds, double X, double Y)
        {
            double[] gt = new double[6];
            ds.GetGeoTransform(gt);
            int px = (int)((X - gt[0]) / gt[1]);
            int py = (int)((Y - gt[3]) / gt[5]);
            return new int[] {px,py};
        }

        /// <summary>
        /// Code Snippet: Read raster block by block
        /// </summary>
        /// <param name="valueRaster"></param>
        private static void ReadRasterBlocks(ref Dataset valueRaster)  
        {  
            Band bandValueRaster = valueRaster.GetRasterBand(1);  
                
            int rasterRows = valueRaster.RasterYSize;  
            int rasterCols = valueRaster.RasterXSize;  
                
            const int blockSize = 1024;  
            for(int row=0; row<rasterRows; row += blockSize)  
            {  
                int rowProcess;  
                if(row + blockSize < rasterRows)  
                {  
                    rowProcess = blockSize;  
                }  
                else  
                {  
                    rowProcess = rasterRows - row;  
                }  
            
                for(int col=0; col < rasterCols; col += blockSize)  
                {  
                    int colProcess;  
                    if(col + blockSize < rasterCols)  
                    {  
                        colProcess = blockSize;  
                    }  
                    else  
                    {  
                        colProcess = rasterCols - col;  
                    }  
            
                    double[] valueRasterValues = new double[rowProcess*colProcess];          
                    bandValueRaster.ReadRaster(col, row, colProcess, rowProcess, valueRasterValues, colProcess,rowProcess, 0, 0);          
                }  
            }  
        }

        /// <summary>
        /// Code Snippet: Read raster row by row
        /// </summary>
        /// <param name="valueRaster"></param>
        private static void ReadRasterRows(ref Dataset valueRaster)  
        {  
            Band bandValueRaster = valueRaster.GetRasterBand(1);  
            int rasterRows = valueRaster.RasterYSize;  
            int rasterCols = valueRaster.RasterXSize;  
            for(int row=0; row <rasterRows; row++)  
            {  
                double[] valueRasterValues = new double[rasterCols];  
                bandValueRaster.ReadRaster(0, row, rasterCols, 1, valueRasterValues, rasterCols, 1, 0, 0);   
            }                                   
        } 

        /* 
        Codificación de shapefiles (probablemente importante cuando hay que crearlos:)
        https://www.programmersought.com/article/9249124261/
        OSGeo.GDAL.Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "NO");
        // In order for the property sheet field to support Chinese, please add the following sentence
        OSGeo.GDAL.Gdal.SetConfigOption("SHAPE_ENCODING", ""); */
        /*
        GeoJson a Shapefile y escritura lectura de GeoTiffs en C#: https://github.com/adadevelopment/adadevfiles/tree/master/files
        GDal rasterize en C#: http://osgeo-org.1560.x6.nabble.com/gdal-dev-GDAL-C-bindings-read-from-vsimem-td5371149.html
        GDAl read and write TIff files C#: https://adadevelopment.github.io/gdal/gdal-read-write.html
        */
    }
}
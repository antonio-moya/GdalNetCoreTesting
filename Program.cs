using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NLog;
using GDAL;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GDalTest
{
    /// <summary>
    /// Todo esto en: https://github.com/bertt/GdalOnNetCoreSample
    /// </summary>
    class Program
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        private static IConfigurationRoot config = null;
        static void Main(string[] args)
        {
            var SO = "WIN";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) SO = "LINUX"; 
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) SO = "WIN";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { Console.WriteLine("MAC not supported"); System.Environment.Exit(0); }

            /* -------------------------------------------------------------------- */
            /*      Read config file  .                                             */
            /* -------------------------------------------------------------------- */
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{SO}.json", true, true)
                .AddJsonFile($"appsettings.{env}.json", true, true)
                .AddEnvironmentVariables();
            config = builder.Build();
            logger.Debug($"Configuration: {config.GetDebugView()}");
            // https://blog.bitscry.com/2017/11/14/reading-lists-from-appsettings-json/
            //List<string> PolygonsLayers = config.GetSection("CATCHMENTS_LAYERS").Get<List<string>>();

            var datapath = config["DATA_PATH"];
            if (!Directory.Exists(datapath)) logger.Error($"No se ha encontrado la ruta de datos {datapath}");

            /* -------------------------------------------------------------------- */
            /*      Configure GDal driver(s).                                       */
            /* -------------------------------------------------------------------- */
            try
            {            
                Gdal.PushErrorHandler (GdalUtils.GDalErrorHandler);
                GdalUtils.Configure();
                Gdal.UseExceptions();   
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.StackTrace + " " + Gdal.GetLastErrorMsg());
            }

            // Lectura de datos de AEMet de GNavarra (radares individuales) y traducción a TIFF
            if (false) {
                var TarsDir = @"C:\XXX\GeoTiffTests\data\GNavarra_AEMet.tar\Radar\RAD_ZAR.2021030.00.tar\";
                foreach(var f in Directory.GetFiles(TarsDir, "*.tar")) {
                    ReadAEMetRadarFile(f, datapath);
                }
                System.Environment.Exit(1);
            }

            // Lectura de datos de AEMet (composición radar) y traducción a GeoTIFF
            if (true) {
                var GZsDir = @"C:\Users\Administrador.000\Desktop\Nueva carpeta\";
                foreach(var f in Directory.GetFiles(GZsDir, "ACUM-RAD-*.gz")) {
                    Console.WriteLine(f);
                    UncompressFiles(f, GZsDir);
                }
                foreach(var f in Directory.GetFiles(GZsDir, "AREA????")) {
                    
                    var output = Path.ChangeExtension(f, ".tiff");
                    if (File.Exists(output)) File.Delete(output);

                    object raster_metedata =  new { 
                        type = "AEMet_radar",
                        ogirin = $"{f}",
                        creation_time_utc = DateTime.Now.ToUniversalTime().ToString("yyyyMMddHHmmss")
                    };
                    AREAnnnToGTiff(f, output, JsonConvert.SerializeObject(raster_metedata), JsonConvert.SerializeObject( new {} ));
                    logger.Info($"Creado {output} desde AREAnnnn ({f})");
                }
                System.Environment.Exit(1);
            }
            // Equal rasters sum
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("===========================SUM rasters (--)==================================");
                var InputRaster = Path.Combine(datapath, "2021036.120000.RAD_ZAR - copia.tiff");
                GdalUtils.SumRasters(Directory.GetFiles(datapath, "2021036.??0000.RAD_ZAR.tiff"), Path.Combine(datapath, "sumaParalelo.tiff"));
            }
            // Coordinates reprojection
            if (true) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("===========================REPROJECTION coordinate testing  (OK)==================================");
                try
                {
                    var ret = GdalUtils.ReprojectCoordinates(23030,4326, 85530d, 446100d, 0d);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.StackTrace + " " +  Gdal.GetLastErrorMsg());
                }
            }
            // Gdal info: información sobre la carga de GDAL
            if (true) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("===========================GDAL INFO==================================");
                try
                {
                    GdalUtils.GetGdalInfo();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }
            // Create TIFF and adding bands
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================Crear GTiff y añadir bandas =====================================");
                try
                {
                    var output = Path.Combine(datapath, "CreateRasterNew.tiff");
                    if (File.Exists(output)) File.Delete(output);

                    int NRows = 200;
                    int Ncols = 100;
                    double MinX = 55;
                    double MinY = 45;
                    double CellSize = 0.1;
                    string src_wkt = GdalUtils.EPSG2WKT(4326);

                    var valores = new List<float[]>();
                    for(var band=0; band<5;band++) {
                        var buffer = new float [NRows * Ncols];
                        for (int i = 0; i < Ncols; i++)
                            for (int j = 0; j < NRows; j++)
                                buffer[i * Ncols + j] = (float)(i * 256 / Ncols) * band;
                        valores.Add(buffer);
                    }
                    GdalUtils.CreateRaster("GTiff", output, NRows,Ncols,MinX, MinY,CellSize, src_wkt, valores, null, null);
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }
            // Raster reprojection
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================Raster reprojection =====================================");
                try
                {
                    int OutEpsg = 23030;
                    var input = Path.Combine(datapath,"CreateRaster.tiff");
                    var output = Path.Combine(datapath, $"CreateRasterNew{OutEpsg}.tiff");
                    if (File.Exists(output)) File.Delete(output);
                    GdalUtils.RasterReprojection(input, output, OutEpsg);
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }
            // GDAL Translate GRIB2 => GTiff
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================Translate GRIB2 => GTiff (OK) =====================================");
                try
                {
                    // Funciona pero tarda mucho:
                    var input = Path.Combine(datapath,"pluviometrosIDW.tiff");
                    var output = Path.Combine(datapath, "pluviometrosIDW.asc");
                    if (File.Exists(output)) File.Delete(output);
                    GdalUtils.TranslateRasterFormat(input, output, "AAIGrid");
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }
            // IDW with gradient correction
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================IDW con gradiente (OK)=====================================");
                try
                {
                    double CellSize = 10000;
                    double xMin = 360000;
                    double yMax = 4830000;
                    int NumCols = 59;
                    int NumRows = 39;
                    double yMin = yMax - (NumRows*CellSize);
                    double xMax = xMin + (NumCols*CellSize);
                    Random random = new Random();
                    double GetRandomNumber(double minimum, double maximum)
                    { 
                        return random.NextDouble() * (maximum - minimum) + minimum;
                    }
                    int NumTermometros = 350;
                    var Points = new List<OSGeo.OGR.Geometry>();
                    // Add more points
                    for(int w=1; w<NumTermometros;w++) {
                            var pnew = new Geometry(wkbGeometryType.wkbPoint);
                            pnew.AddPointZM(
                                GetRandomNumber(xMin, xMax), 
                                GetRandomNumber(yMin, yMax), 
                                GetRandomNumber(100, 300), 
                                GetRandomNumber(0, 10));
                            Points.Add(pnew);
                    }
                    SurfaceInterpolations.IdwTemperaturesWithElevationCorrection(Path.Combine(datapath, $"IdwTemperaturesWithElevationCorrection_{Points.Count}.tiff"), Points);

                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }
            // IDW with NearestNeighbour
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================IDW NN (OK)=====================================");
                try
                {
                    SurfaceInterpolations.IDWwithNearestNeighbour(Path.Combine(datapath, "pluviometros_23030.shp"),Path.Combine(datapath, "pluviometrosIDW.tiff"));
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }
            // Create contour
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================Contour (OK)=====================================");
                try
                {
                    var input = Path.Combine(datapath, "pluviometrosIDW.tiff");
                    var output = Path.Combine(datapath, "contour.shp");
                    if (File.Exists(input)) File.Delete(input);
                    if (File.Exists(output)) File.Delete(output);
                    GdalUtils.Contour(input, output, 1d, 0d);
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }
            // Raster info: Geotiff multiband
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("=============================Info GEOTIFF Multiband======================================");
                GDALInfo.Info(Path.Combine(datapath,"CHEBROe00.20201125.tif"), false);
            }
            // Raster info: GRIB2 multiband
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("==============================Info GRIB2=====================================");
                GDALInfo.Info(Path.Combine(datapath,"CHEBROe00.20201125.grib2"), true);   
            }

        }

        public static void ReadAEMetRadarFile(string TarFilePath, string OutputDir) {

            /* -------------------------------------------------------------------- */
            /*      Read AEMet radar files                                          */
            /* -------------------------------------------------------------------- */
            // descomprimir ficheros y transforma de AREAnnnn a GeoTiff
            string directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            UncompressFiles(TarFilePath, directoryPath);
            foreach(var f in Directory.GetFiles(directoryPath, "*.A01-A-N.gz")) {
                UncompressFiles(f, directoryPath);
            }

            // Umcompressed AREAnnn files to GeoTIFF
            foreach(var f in Directory.GetFiles(directoryPath, "*.A01-A-N")) {
                Console.WriteLine(f);                
                var output = Path.ChangeExtension(f, ".tiff");
                if (File.Exists(output)) File.Delete(output);

                object raster_metedata =  new { 
                    type = "AEMet_radar",
                    ogirin = $"{f}",
                    creation_time_utc = DateTime.Now.ToUniversalTime().ToString("yyyyMMddHHmmss")
                };
                AREAnnnToGTiff(f, output, JsonConvert.SerializeObject(raster_metedata), JsonConvert.SerializeObject( new {} ));

                var result = Path.Combine(OutputDir, Path.GetFileName(output));
                File.Move(output, result);
                logger.Info($"Creado {result} desde AREAnnnn ({TarFilePath})");
            }
            if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, true);
        }

        private static void AREAnnnToGTiff(string input, string output, string raster_medatada, string band_metadata) {
            
            var a = new AREAnnnnFile.AREAnnnn(input);
            int NRows = a.getNumFilas;
            int NCols = a.getNumCols;
            
            double dX = 1000d * a.getResCols * a.GetXSpace();
            //double dY = 1000d * a.getResFilas * a.GetXSpace(); no es necesario, se asume malla cuadrada.
            double MinX=0, MinY=0;
            a.File2Coods(ref MinX, ref MinY, a.getNumFilas,1);                

            // Datos de la malla en formato "GDAL"
            var d = a.GetDatos();
            var datos = new float[a.getNumFilas*a.getNumCols];
            var cont = 0;
            for (int i = a.getNumFilas-1; i >=0; i--) {
                for (int j = 0; j < a.getNumCols; j++) {
                    datos[cont] = d[i,j];
                    cont++;
                }
            }
            // Sistema de coordenadas
            string EsriWkt = config["RADAR_AEMET:PROJ_ESRI_WKT"];
            GdalUtils.CreateRaster("GTiff", output, NRows, NCols, MinX, MinY, dX, EsriWkt, new List<float[]>() { datos }, raster_medatada, new List<string>() { band_metadata } );
        }

        //https://stackoverflow.com/questions/8863875/decompress-tar-files-using-c-sharp
        private static void UncompressFiles(string tarFilePath, string directoryPath)
        {
            using (Stream stream = File.OpenRead(tarFilePath))
            {
                var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        var opt = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };
                        reader.WriteEntryToDirectory(directoryPath, opt);
                    }
                }
            }
        }

    }
}

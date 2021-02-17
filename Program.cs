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
            List<string> PolygonsLayers = config.GetSection("CATCHMENTS_LAYERS").Get<List<string>>();

            /* -------------------------------------------------------------------- */
            /*      Configure GDal driver(s).                                       */
            /* -------------------------------------------------------------------- */
            try
            {            
                Gdal.PushErrorHandler (new Gdal.GDALErrorHandlerDelegate (GdalUtils.GDalErrorHandler));
                GdalUtils.Configure();
                Gdal.UseExceptions();   
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.StackTrace + " " + Gdal.GetLastErrorMsg());
            }

            var datapath = config["DATA_PATH"];

            if (false) {
                var TarsDir = @"C:\XXX\GeoTiffTests\data\GNavarra_AEMet.tar\Radar\RAD_ZAR.2021030.00.tar\";
                foreach(var f in Directory.GetFiles(TarsDir, "*.tar")) {
                    ReadAEMetRadarFile(f, datapath);
                }
                System.Environment.Exit(1);
            }
            
            if (true) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("===========================REPROJECTION coordinate testing  (OK)==================================");
                try
                {
                    GdalUtils.ReprojectCoordinatesExample();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.StackTrace + " " +  Gdal.GetLastErrorMsg());
                }
            }

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

            if (true) {
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

            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================IDW (OK)=====================================");
                try
                {
                    GdalUtils.IdwInterpolation(Path.Combine(datapath, "pluviometros_23030.shp"),Path.Combine(datapath, "pluviometrosIDW.tiff"));
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }

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

            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("=============================Info GEOTIFF Multiband======================================");
                GDALInfo.Info(Path.Combine(datapath,"CHEBROe00.20201125.tif"), false);
            }
            if (false) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("==============================Info GRIB2=====================================");
                GDALInfo.Info(Path.Combine(datapath,"CHEBROe00.20201125.grib2"), true);   
            }
            if (false) {
                //SepararEnBandas(Path.Combine(datapath,"CHEBROe00.20201125.grib2"));    
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
            
            //https://stackoverflow.com/questions/8863875/decompress-tar-files-using-c-sharp
            void UncompressFiles(string tarFilePath, string directoryPath)
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

            void AREAnnnToGTiff(string input, string output, string raster_medatada, string band_metadata) {
                
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

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using NLog;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;

namespace Test
{
    /// <summary>
    /// Todo esto en: https://github.com/bertt/GdalOnNetCoreSample
    /// </summary>
    class Program
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        static void Main(string[] args)
        {

            /* -------------------------------------------------------------------- */
            /*      Read config file  .                                             */
            /* -------------------------------------------------------------------- */
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var builder = new ConfigurationBuilder()
                .AddJsonFile($"appsettings.json", true, true)
                .AddJsonFile($"appsettings.{env}.json", true, true)
                .AddEnvironmentVariables();
            var config = builder.Build();

            /* -------------------------------------------------------------------- */
            /*      Register driver(s).                                             */
            /* -------------------------------------------------------------------- */
            try
            {            
                Gdal.PushErrorHandler (new Gdal.GDALErrorHandlerDelegate (GdalUtils.GDalErrorHandler));
                GdalUtils.Configure(config["GDAL:PATH_GDAL_BIN"], config["GDAL:PATH_GDAL_DRIVER"], config["GDAL:PATH_GDAL_DATA"]);
                Gdal.UseExceptions();   
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.StackTrace + " " + Gdal.GetLastErrorMsg());
            }

            AKIII: leer congfigs json en función del SO
            var datapath = config["DATA_PATH"];
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) datapath = "/home/local/NILO/antonio/Escritorio/GeoTiffTests/data/";
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) datapath = @"C:\XXX\GeoTiffTests\data\";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) { Console.WriteLine("MAC not supported"); System.Environment.Exit(0); }

            if (true) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("===========================REPROJECTION coordinate testing==================================");
                try
                {
                    GdalUtils.ReprojectCoordinatesExample();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, ex.StackTrace + " " +  Gdal.GetLastErrorMsg());
                }
            }

           if (false) {
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
                    int EPSG = 4326;

                    var valores = new List<float[]>();
                    for(var band=0; band<5;band++) {
                        var buffer = new float [NRows * Ncols];
                        for (int i = 0; i < Ncols; i++)
                            for (int j = 0; j < NRows; j++)
                                buffer[i * Ncols + j] = (float)(i * 256 / Ncols) * band;
                        valores.Add(buffer);
                    }
                    GdalUtils.CreateRaster(output, NRows,Ncols,MinX, MinY,CellSize, EPSG, valores, null, null);
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, Gdal.GetLastErrorMsg());
                }
            }

            if (true) {
                Console.WriteLine("===================================================================");
                Console.WriteLine("========================Raster reprojection =====================================");
                try
                {
                    int OutEpsg = 23030;
                    var input = Path.Combine(datapath,"CreateRasterNew.tiff");
                    var output = Path.Combine(datapath, $"CreateRasterNew{OutEpsg}.tiff");
                    if (File.Exists(output)) File.Delete(output);
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
                Console.WriteLine("========================Translate GRIB2 => GTiff =====================================");
                try
                {
                    var input = "";
                    var output = "";
                    /*
                    // Funciona pero tarda mucho:
                    input = Path.Combine(datapath,"CHEBROe00.20201125.grib2");;
                    output = Path.Combine(datapath, "CHEBROe00.20201125.tiff");
                    if (File.Exists(output)) File.Delete(output);
                    GDALSpatialInterpolation.TranslateRasterFormat(input, output, "GTiff");
                    */
                    input = Path.Combine(datapath,"pluviometrosIDW.tiff");
                    output = Path.Combine(datapath, "pluviometrosIDW.asc");
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
                Console.WriteLine("========================IDW=====================================");
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
                Console.WriteLine("========================Contour=====================================");
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
    }
}

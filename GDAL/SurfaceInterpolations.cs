using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using NLog;
using OSGeo.GDAL;
using OSGeo.OGR;
using System.Linq;

namespace GDAL
{
    /// <summary>
    /// 
    /// </summary>
    class SurfaceInterpolations
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Performs IDW with NN surface interpolation
        /// </summary>
        /// <param name="InputVector"></param>
        /// <param name="OutputTIFF"></param>
        public static void IDWwithNearestNeighbour(string InputVector, string OutputTIFF) {

            // No se realiza ningún tipo de reproyección, el TIFF se genera con el mismo sistema de coordenadas que el vectorial de entrada

            // Dimensiones del raster de salida (todas las coordenadas se establecel en unidades del sistema de coordinadas del vectorial de entrada)
            double CellSize = 10000;
            double xMin = 360000;
            double yMax = 4830000;
            int NumCols = 59;
            int NumRows = 39;
            double yMin = yMax - (NumRows*CellSize);
            double xMax = xMin + (NumCols*CellSize);

            //-----------------------------
            // Parámetros de interpolación
            //-----------------------------
            var cul = System.Globalization.CultureInfo.InvariantCulture;
            var parameters = new List<string>();
            parameters.AddRange(new string[] {"-zfield", "rainfall"}); // Campo con datos para interpolar
            parameters.AddRange(new string[] {"-txe", xMin.ToString(cul),xMax.ToString(cul)});
            parameters.AddRange(new string[] {"-tye", yMin.ToString(cul),yMax.ToString(cul)});
            parameters.AddRange(new string[] {"-outsize", NumCols.ToString(cul),NumRows.ToString(cul)});
            // algoritmo a utilizar (https://gdal.org/programs/gdal_grid.html#interpolation-algorithms)
            double radious = Math.Max((xMax-xMin)/2, (yMax-yMin)/2);
            parameters.AddRange( new string[] {"-a", $"invdistnn:power=2.0:smothing=0.0:radius={radious.ToString(cul)}:max_points=12:min_points=5:nodata=0.0"});
            parameters.AddRange(new string[] {"-of", "gtiff"}); // formato de salida
            parameters.AddRange(new string[] {"-ot", "Float32"}); // tipo de datos de salida
            parameters.AddRange(new string[] {"--config", "GDAL_NUM_THREADS ALL_CPUS"}); // una u otra, no se sabe
            parameters.AddRange(new string[] {"--config", "GDAL_NUM_THREADS=ALL_CPUS"}); // una u otra, no se sabe
 
            logger.Trace("Parámetros: " + string.Join(" ", parameters));

            //-----------------------------
            // Vectorial de entrada
            // Si el vectorial tiene algún valor no válido deben ser limpiados aquí
            // Ejecución del algoritmo
            //-----------------------------
            using(var ds = Gdal.OpenEx(InputVector, 0, null, null, null)) {
                
                var gridDS =  Gdal.wrapper_GDALGrid(OutputTIFF, ds, new GDALGridOptions(parameters.ToArray()), (Gdal.GDALProgressFuncDelegate) GdalUtils.GDalProgress, string.Empty);
                gridDS.SetDescription("SUAT.IDW from pluviometers");
                //gridDS.SetMetadata( {"": '1', 'key2': 'yada'} );
            }
        }

        /// <summary>
        /// Computes an IDW surface interpolation using a gradient to take into account differces from a raster
        /// Usefull for climatological data as perform a temperature correction based on terrain elevation
        /// </summary>
        /// <param name="OutputGTiffFile"></param>
        public static void IdwTemperaturesWithElevationCorrection(string OutputGTiffFile, List<Geometry> Points) {

            // Datos del malla de salida
            int EPSG = 23030;
            double CellSize = 10000;
            double CellSizeX = CellSize;
            double CellSizeY = CellSize;
            double xMin = 360000;
            double yMax = 4830000;
            int NumCols = 59;
            int NumRows = 39;
            double yMin = yMax - (NumRows*CellSize);
            double xMax = xMin + (NumCols*CellSize);
            
            var CorrectionGradient = -0.65d;
            var IdwExponent = 3d;

            //---------------------------------------------
            // Filter points with NoData in value or in elevation
            //---------------------------------------------
            Points = Points.Where(po => !double.IsNaN(po.GetM(0)) && !double.IsNaN(po.GetZ(0))).ToList();
            logger.Trace($"INICIANDO con {Points.Count} termómetros.");
            //---------------------------------------------
            // Compute weights for each point and raster cell
            //---------------------------------------------
            var weights = new Dictionary<Geometry, double[,]>();
            var locker = new object();
            Parallel.ForEach(Points, (p) => {
                
                double dXPunto = p.GetX(0);
                double dYPunto = p.GetY(0);

                // Para cada celda del mallado de la aplicación
                double[,] coeficients = new double[NumRows, NumCols];
                for (int i = 0; i < NumRows; i++)
                {
                    for (int j = 0; j < NumCols; j++)
                    {
                        double CellX = xMin + (j * CellSizeX) + (CellSizeX / 2);
                        double CellY = yMax - ((i * CellSizeY) + (CellSizeY / 2));
                        // Se calcula la distancia entre celda y punto como número de pasos, para evitar valores muy grandes
                        double distancia = System.Math.Sqrt(System.Math.Pow((dXPunto - CellX), 2) / (CellSizeX) + System.Math.Pow((dYPunto - CellY), 2) / (CellSizeY));
                        distancia = Math.Max(distancia, 0.0000001d);
                        // Se calculan coeficientes, multiplicados por una costante, para evitar valores muy pequeños
                        coeficients[i, j] = (1 / (System.Math.Pow(distancia, IdwExponent))) * (10 ^ 3);

                    }
                }
                lock(locker) weights.Add(p, coeficients);
            });
            logger.Trace($"Pesos computados.");

            //---------------------------------------------
            // Compute elevations for each raster cell from DEM
            // AKIIII => read with gdal, retornar double.NAN en caso de nulo
            //---------------------------------------------
            double GetElevation(double x, double y) {
                return 200d;
            };

            //---------------------------------------------
            // Compute raster values
            //---------------------------------------------
            var ResultData = new float[NumRows, NumCols];
            for (int i = 0; i < NumRows; i++)
                for (int j = 0; j < NumCols; j++)
                    ResultData[i,j] = float.NaN;

            double CellValue;
            double WeightsSum;// Acumula la suma de los coeficientes válidos para luego dividir por ella
            //------------------------------------------------------------
            // Iterar por todas las celdas del mallado a crear
            //------------------------------------------------------------
            for (int i = 0; i < NumRows; i++)
            {
                logger.Trace($"Tratando fila {i} de {NumRows}.");
                for (int j = 0; j < NumCols; j++)
                {
                    // Inicializar
                    CellValue = 0d;
                    WeightsSum = 0d;

                    //------------------------------------------------------------
                    // Calcular el valor para la celda i,j
                    //------------------------------------------------------------
                    // Obtner el sumatorio de los C1V1 + C2V2 + ... + CnVn
                    foreach(var p in Points)
                    {
                        double dValAux = p.GetM(0);
                        double Elevation = p.GetZ(0);
                        if (!double.IsNaN(dValAux))
                        {
                            //-------------------------------------------------------------------------
                            //  Aplicamos corrección de valor por cota:
                            // Búsqueda de la cota de la celda
                            //-------------------------------------------------------------------------
                            double dCeldaX = xMin  + (i * CellSizeX) + (CellSizeX / 2d);
                            double dCeldaY = yMax  - ((j * CellSizeY) + (CellSizeY / 2d));

                            // Get current cell elevation
                            double CellElevation = GetElevation(dCeldaX, dCeldaY);

                            //------------------------------------------------------------------------------------------
                            // No se aplica el gradiente termométrico a las celdas sobre las que no tenemos cota
                            //------------------------------------------------------------------------------------------    
                            if (!double.IsNaN(CellElevation))
                            {
                                //---------------------------------------------
                                // Cálculo del valor de temperatura modificado
                                //---------------------------------------------
                                dValAux += CorrectionGradient * (CellElevation - Elevation) / 100;
                            }
                            double weight = weights[p][i,j]; 
                            CellValue += weight * dValAux;
                            WeightsSum += weight;
                        }
                    }
                    // Obtener el valor dividiendo por SUM(C1 ... Cn)
                    if (WeightsSum != 0) ResultData[i,j] = (float) (CellValue / WeightsSum);

                }
            }

            // Create results raster
            logger.Trace($"Creando gtif...");
            var datos = new float[NumRows*NumCols];
            var cont = 0;
            for (int i = NumRows-1; i >=0; i--) {
                for (int j = 0; j < NumCols; j++) {
                    datos[cont] = ResultData[i,j];
                    cont++;
                }
            }

            // Create output GTiff
            if (File.Exists(OutputGTiffFile)) File.Delete(OutputGTiffFile);
            string EsriWkt = GdalUtils.EPSG2WKT(EPSG);
            GdalUtils.CreateRaster("GTiff", OutputGTiffFile, NumRows, NumCols, xMin, yMin, CellSize, EsriWkt, new List<float[]>() { datos }, null, null );
            logger.Trace($"Creado {OutputGTiffFile}");
        }
    }
}
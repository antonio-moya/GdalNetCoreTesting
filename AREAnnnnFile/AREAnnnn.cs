using System;
using System.IO;
using System.Text;
using NLog;

namespace AREAnnnnFile
{
    /// <summary>
    /// AREAnnnn: encapsula la información contenida en un fichero
    /// AREA de McIDAS.
    /// </summary>
    public class AREAnnnn
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        
        protected bool IsLittleEndian = false;

        protected string AREAFile = string.Empty;

        // Variables para las propiedades que son muy utilizadas 

        private int _ULCoordCol = -999;
        private int _ULCoordFila = -999;
        private int _NumFilas = -999;
        private int _NumCols = -999;
        private int _ResFilas = -999;
        private int _ResCols = -999;
        private int _NavBlockOffset = -999;
        private int _DataBlockOffset = -999;
        private double _dXSpace = -999;
        private int _lPoleRow = -999;
        private int _lPoleCol = -999;
        private double _dPoleLat = -999;
        private double _dPoleLon = -999;

        // VALORES NULOS SEGÚN LA LONGITUD DEL DATO
        public static byte NULLVALUE_BYTE = 255;

        public AREAnnnn(string sAREAFile)
        {
            AREAFile = sAREAFile;
        }

        /* TODO ERROR: Skipped RegionDirectiveTrivia */
        /// <summary>
        /// Con las coordenadas del fichero (fila y columna)
        /// * devuelve las coordenadas geográficas (imagen)
        /// * fila y con van de (1,1)  a (NumFil,NumCol)
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="Fila"></param>
        /// <param name="Col"></param>
        /// <remarks></remarks>
        public void File2Coods(ref double X, ref double Y, int Fila, int Col)
        {
            double dX;
            double dY;
            int lPoleRow = -999;
            int lPoleCol = -999;
            double lPoleLat = -999;
            double lPoleLon = -999;
            double dXSpace = -999;
            dXSpace = GetXSpace();
            GetPoleInfo(ref lPoleRow, ref lPoleCol, ref lPoleLat, ref lPoleLon);
            double dStLine = getULCoordFila;
            double dStElem = getULCoordCol;
            double lRowRes = getResFilas;
            double lColRes = getResCols;
            dX = dStElem + (Col - 1) * lColRes;
            dX = (dX - lPoleCol) * dXSpace * 1000d;
            dY = dStLine + (Fila - 1) * lRowRes;
            dY = -(dY - lPoleRow) * dXSpace * 1000d;
            X = dX;
            Y = dY;
        }

        public void Coods2File(double X, double Y, ref int Fila, ref int Col)
        {
            double dX;
            double dY;
            int lPoleRow = -999;
            int lPoleCol = -999;
            double lPoleLat = -999;
            double lPoleLon = -999;
            double dXSpace = -999;
            dXSpace = GetXSpace();
            GetPoleInfo(ref lPoleRow, ref lPoleCol, ref lPoleLat, ref lPoleLon);
            dX = X;
            dY = Y;
            double dStLine = getULCoordFila;
            double dStElem = getULCoordCol;
            double lRowRes = getResFilas;
            double lColRes = getResCols;
            dX = dX / (1000d * dXSpace);
            double dCol = (dX - dStElem + lPoleCol + lColRes) / lColRes;
            dY = dY / (1000d * dXSpace);
            double dFila = (lPoleRow - dStLine - dY + lRowRes) / lRowRes;
            Col = (int)Math.Round(dCol);
            Fila = (int)Math.Round(dFila);
        }
        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        /* TODO ERROR: Skipped RegionDirectiveTrivia */
        /// <summary>
        /// getLongitudDatosLinea retorna la longitud de los datos que contiene * una fila
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public int getLongitudDatosLinea
        {
            get
            {
                return getNumBandas * getNumCols * getBytesPorPunto;
            }
        }

        /// <summary>
        /// getLongitudPrefijoLinea retorna la longitud del prefijo * una fila
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public int getLongitudPrefijoLinea
        {
            get
            {
                int valor = getPrefijoDocumentacionLong;
                valor += getPrefijoCalibracionLong;
                valor += getPrefijoListaBandasLong;
                valor += getValCodeLong;
                return valor;
            }
        }

        /// <summary>
        /// getLongitudLinea retorna la longitud de una linea completa
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public int getLongitudLinea
        {
            get
            {
                return getLongitudPrefijoLinea + getLongitudDatosLinea;
            }
        }

        /// <summary>
        /// getLongitudBloqueDatos retorna la longitud del bloque de datos completo
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public int getLongitudBloqueDatos
        {
            get
            {
                return getNumFilas * getLongitudLinea;
            }
        }

        /// <summary>
        /// Retorna la resolución de la imagen
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public int getResImage
        {
            get
            {
                return getResCols * getResFilas;
            }
        }

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        /* TODO ERROR: Skipped RegionDirectiveTrivia */
        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getDateTime retorna la fecha y hora de la creación del fichero (datos de la cabecera)
        // * Pos 4 y 5
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public DateTime getDateTime
        {
            get
            {
                string date = getIntVal(3 * 4).ToString("000000");
                string time = getIntVal(4 * 4).ToString("000000");
                int anno = int.Parse(date.Substring(0, 3)) + 1900;
                int diaanno = int.Parse(date.Substring(3, 3));
                int hora = int.Parse(time.Substring(0, 2));
                int minuto = int.Parse(time.Substring(2, 2));
                int segundo = 0;
                var fecha = new DateTime(anno, 1, 1, hora, minuto, segundo);
                var result = fecha.AddDays(diaanno - 1);
                return result;
            }
        }
        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getULCoordFila retorna la coordenada de la fila superior izquierda
        // * Pos 6
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getULCoordFila
        {
            get
            {
                if (_ULCoordFila == -999)
                {
                    _ULCoordFila = getIntVal(5 * 4);
                }

                return _ULCoordFila;
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getULCoordCol retorna la coordenada de la columna superior izquierda
        // * Pos 7
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getULCoordCol
        {
            get
            {
                if (_ULCoordCol == -999)
                {
                    _ULCoordCol = getIntVal(6 * 4);
                }

                return _ULCoordCol;
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * GetPointsPerLine retorna el número de puntos por línea de datos
        // * Pos 9
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getNumFilas
        {
            get
            {
                if (_NumFilas == -999)
                {
                    _NumFilas = getIntVal(8 * 4);
                }

                return _NumFilas;
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * GetPointsPerLine retorna el número de puntos por línea de datos
        // * Pos 10
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getNumCols
        {
            get
            {
                if (_NumCols == -999)
                {
                    _NumCols = getIntVal(9 * 4);
                }

                return _NumCols;
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getBytesPorPunto retorna el número de bytes que ocupa cada punto de datos
        // * Pos 11
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getBytesPorPunto
        {
            get
            {
                return getIntVal(10 * 4);
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * gesResFilas retorna la resolución de las filas
        // * Pos 12
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getResFilas
        {
            get
            {
                if (_ResFilas == -999)
                {
                    _ResFilas = getIntVal(11 * 4);
                }

                return _ResFilas;
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * gesResCols retorna la resolución de las filas
        // * Pos 13
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getResCols
        {
            get
            {
                if (_ResCols == -999)
                {
                    _ResCols = getIntVal(12 * 4);
                }

                return _ResCols;
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getNumBandas retorna el número de bandas espectrales
        // * Pos 14
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getNumBandas
        {
            get
            {
                return getIntVal(13 * 4);
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getDataBlockOffset retorna el byte en el que empieza el bloque de datos
        // * Pos 34
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getDataBlockOffset
        {
            get
            {
                if (_DataBlockOffset == -999)
                {
                    _DataBlockOffset = getIntVal(33 * 4);
                }

                return _DataBlockOffset;
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * GetNavBlockOffset retorna el byte en el que empieza el bloque de navegacion
        // * Pos 35
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getNavBlockOffset
        {
            get
            {
                if (_NavBlockOffset == -999)
                {
                    _NavBlockOffset = getIntVal(34 * 4);
                }

                return _NavBlockOffset;
            }
        }
        // GetNavBlockOffset


        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getValCodeLong retorna la longitud de prefijo del código de validación,
        // * si no es cero la longitud es cuatro.
        // * Pos 36
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getValCodeLong
        {
            get
            {
                int valor = getIntVal(35 * 4);
                // Pos 36
                if (valor != 0)
                {
                    valor = 4;
                }

                return valor;
            }
        }
        // GetNavBlockOffset


        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getPrefijoDocumentacionLong retorna la lontigud del prefijo de documentación
        // * Pos 49
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getPrefijoDocumentacionLong
        {
            get
            {
                return getIntVal(48 * 4);
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getPrefijoCalibracionLong retorna la lontigud del prefijo de calibración
        // * Pos 50
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getPrefijoCalibracionLong
        {
            get
            {
                return getIntVal(49 * 4);
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getPrefijoListaBandasLong retorna la lontigud del prefijo de la lista de bandas
        // * Pos 51
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public int getPrefijoListaBandasLong
        {
            get
            {
                return getIntVal(50 * 4);
            }
        }

        // '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
        // * getTipoFuente retona el tipo de fuente (específico del satélite)
        // * Pos 52
        // *''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

        public string getTipoFuente
        {
            get
            {
                return getStringVal(51 * 4, 4);
            }
        }

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        /* TODO ERROR: Skipped RegionDirectiveTrivia */
        /// <summary>
        /// getProjectionType retona el tipo de proyección
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public string getProjectionType()
        {
            int lNavBlockOffSet = getNavBlockOffset;
            return getStringVal(lNavBlockOffSet, 4);
        }


        /// <summary>
        /// GetPoleInfo obtiene las coordenadas de imagen y lat/lon del polo de la malla pasada en sFile
        /// </summary>
        /// <param name="lPoleRow"></param>
        /// <param name="lPoleCol"></param>
        /// <param name="dPoleLat"></param>
        /// <param name="dPoleLon"></param>
        /// <remarks></remarks>
        public void GetPoleInfo(ref int lPoleRow, ref int lPoleCol, ref double dPoleLat, ref double dPoleLon)
        {
            // Si no estan calculados se calculan
            if (_lPoleRow == -999 || _lPoleCol == -999 || _dPoleLat == -999 || _dPoleLon == -999)
            {
                // Esta información se encuentra en el bloque de navegación del fichero
                int lNavBlockOffSet = getNavBlockOffset;
                _lPoleRow = getIntVal(lNavBlockOffSet + 4);
                _lPoleCol = getIntVal(lNavBlockOffSet + 8);

                // xqlon
                int aux = getIntVal(lNavBlockOffSet + 24);
                int iwest = getIntVal(1);
                if (iwest >= 0)
                {
                    iwest = 1;
                }

                _dPoleLon = Int32LatLonToDouble(aux);
                if (iwest == 1)
                {
                    _dPoleLon = -_dPoleLon;
                }

                // xpole
                int ipole = getIntVal(lNavBlockOffSet + 36);
                if (ipole == 0)
                {
                    ipole = 900000;
                }

                _dPoleLat = Int32LatLonToDouble(ipole);
            }

            lPoleRow = _lPoleRow;
            lPoleCol = _lPoleCol;
            dPoleLat = _dPoleLat;
            dPoleLon = _dPoleLon;
        }
        // GetPoleInfo

        /// <summary>
        /// GetLats retorna los valores de Lat1 y Lat2, paralelos usados para la proyeccion Lambert
        /// </summary>
        /// <param name="dLat1"></param>
        /// <param name="dLat2"></param>
        /// <remarks></remarks>
        public void GetLats(ref double dLat1, ref double dLat2)
        {
            int lNavBlockOffSet = getNavBlockOffset;
            dLat1 = Int32LatLonToDouble(getIntVal(lNavBlockOffSet + 12));
            dLat2 = Int32LatLonToDouble(getIntVal(lNavBlockOffSet + 16));
        }
        // GetLats

        /// <summary>
        /// GetXSpace retorna el paso de la malla de sFile (en Kilómetros)
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public double GetXSpace()
        {
            if (_dXSpace == -999)
            {
                int lNavBlockOffSet = getNavBlockOffset;
                _dXSpace = (double)getIntVal(lNavBlockOffSet + 20) / 1000;
            }

            return _dXSpace;
        }
        // GetXSpace
        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */
        /* TODO ERROR: Skipped RegionDirectiveTrivia */
        /// <summary>
        /// Conversor de valores de longitud/latitud de Long (como viene en el fichero
        /// ' radar) a Double (valor en grados)
        /// </summary>
        /// <param name="valor"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public double Int32LatLonToDouble(int valor)
        {
            int val;
            double aux1;
            double aux2;
            double aux3;
            double dvalor;
            if (valor < 0)
            {
                val = -valor;
            }
            else
            {
                val = valor;
            }

            aux1 = (int)Math.Round((double)val / 10000);
            aux2 = (double)val / 100 % 100 / 60;
            aux3 = (double)val % 100 / 3600;
            dvalor = aux1 + aux2 + aux3;
            if (valor < 0)
            {
                return -dvalor;
            }
            else
            {
                return dvalor;
            }
        }

        /// <summary>
        /// Retorna el valor entero que se encuentre en el byte
        /// indicada como parámetro
        /// </summary>
        /// <param name="Pos"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private int getIntVal(int Pos)
        {
            var fs = new FileStream(AREAFile, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs);

            // Movemos Pos bytes
            br.BaseStream.Seek(Pos, SeekOrigin.Begin);
            // Lectura de la palabra
            int valor = br.ReadInt32();
            if (this.IsLittleEndian)
            {
                // Transformar LittleEndian => BigEndian
                var temp = BitConverter.GetBytes(valor);
                Array.Reverse(temp);
                valor = BitConverter.ToInt32(temp, 0);
            }

            br.Close();
            fs.Close();
            return valor;
        }

        /// <summary>
        /// Retorna el valor string que se encuentre en el byte
        /// indicada como parámetro y con la longitud (en bytes) requerida
        /// </summary>
        /// <param name="Pos"></param>
        /// <param name="Long"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private string getStringVal(int Pos, int Long)
        {
            var fs = new FileStream(AREAFile, FileMode.Open, FileAccess.Read);
            var br = new BinaryReader(fs);

            // Movemos Pos bytes
            br.BaseStream.Seek(Pos, SeekOrigin.Begin);
            var chs = br.ReadChars(Long);
            var sb = new StringBuilder(Long);
            sb.Append(chs);
            br.Close();
            fs.Close();
            return sb.ToString();
        }
        // GetStringVal

        /* TODO ERROR: Skipped EndRegionDirectiveTrivia */

        /// <summary>
        /// Devuelve una malla(array bidimensional) de valores, si el fichero no es válido devuelve
        /// la malla con valores nulos
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        public float[,] GetDatos()
        {
            int NumFils = getNumFilas;
            int NumCols = getNumCols;
            var Datos = new float[NumFils, NumCols];
            
            FileStream fs = null;
            BinaryReader br = null;
            try
            {
                int ByteInicioBloqueDatos = this.getDataBlockOffset;
                fs = new FileStream(AREAFile, FileMode.Open, FileAccess.Read);
                br = new BinaryReader(fs);

                // Movemos al inicio del bloque de datos
                br.BaseStream.Seek(ByteInicioBloqueDatos, SeekOrigin.Begin);

                // FILAS
                for (int i = 0; i < NumFils; i++)
                {
                    // COLUMNAS
                    for (int j = 0; j < NumCols; j++)
                    {
                        var bytes = br.ReadBytes(2);
                        float val = -9999f;

                        // Transformar valores nulos
                        if (bytes[0] != 255 | bytes[1] != 255)
                        {
                            if (this.IsLittleEndian)
                            {
                                val = BytesToSingle(bytes[0], bytes[1]);
                            }
                            else
                            {
                                val = BytesToSingle(bytes[1], bytes[0]);
                            }
                        }

                        Datos[i, j] = val;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                throw ex;
            }

            try
            {
                if (br is object)
                    br.Close();
                if (fs is object)
                    fs.Close();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }

            return Datos;
        }

        /// <summary>
        /// Datos los dos bytes leídos del fichero McIDAS obtiene el valor según la
        /// codificación utilizada por la AEMET
        /// </summary>
        /// <param name="byte0"></param>
        /// <param name="byte1"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private float BytesToSingle(byte byte0, byte byte1)
        {
            int Exponent = byte0 >> 4;
            int Mantissa = 0;
            Mantissa = Mantissa | byte0 & 0xF;
            Mantissa <<= 8;
            Mantissa = Mantissa | byte1;
            if (Exponent != 0)
            {
                Mantissa = Mantissa | 0x1000;
                Mantissa = Mantissa << Exponent - 1;
            }
            return (float)(Mantissa / 1000d);
        }

    }
}
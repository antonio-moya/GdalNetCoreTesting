using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NLog;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;

namespace GDalTest
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
    }
}
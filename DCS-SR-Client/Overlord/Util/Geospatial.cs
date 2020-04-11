using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Util
{
    class Geospatial
    {
        const double EarthRadius = 6378137.0;
        const double DegreesToRadians = 0.0174532925;
        const double RadiansToDegrees = 57.2957795;
        /// <summary>
        /// Calculates the new-point from a given source at a given range (meters) and bearing (degrees).
        /// Taken from https://adhamhurani.blogspot.com/2010/08/c-how-to-add-distance-and-calculate-new.html
        /// </summary>
        /// <param name="source">Orginal Point</param>
        /// <param name="range">Range in meters</param>
        /// <param name="bearing">Bearing in degrees</param>
        /// <returns>End-point from the source given the desired range and bearing.</returns>
        public static Point CalculatePointFromSource(Point source, double range, double bearing)
        {
            double latA = source.Y * DegreesToRadians;
            double lonA = source.X * DegreesToRadians;
            double angularDistance = range / EarthRadius;
            double trueCourse = bearing * DegreesToRadians;

            double lat = Math.Asin(Math.Sin(latA) * Math.Cos(angularDistance) + Math.Cos(latA) * Math.Sin(angularDistance) * Math.Cos(trueCourse));

            double dlon = Math.Atan2(Math.Sin(trueCourse) * Math.Sin(angularDistance) * Math.Cos(latA), Math.Cos(angularDistance) - Math.Sin(latA) * Math.Sin(lat));
            double lon = ((lonA + dlon + Math.PI) % (Math.PI * 2)) - Math.PI;

            return new Point(lon * RadiansToDegrees, lat * RadiansToDegrees);
        }
    }
}

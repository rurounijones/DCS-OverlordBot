using System;
using Geo;
using Geo.Abstractions.Interfaces;
using Geo.Geodesy;
using Geo.Geomagnetism;
using Geo.Geometries;
using NLog;

namespace RurouniJones.DCS.OverlordBot.Util
{
    public class Geospatial
    {
        // Fudge factor to try and bring the bot inline with what we are seeing in game.
        public static readonly double CaucasusFudgeFactor = 1.5;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private const double EarthRadius = 6378137.0;
        private const double DegreesToRadians = 0.0174532925;
        private const double RadiansToDegrees = 57.2957795;
        /// <summary>
        /// Calculates the new-point from a given source at a given range (meters) and bearing (degrees).
        /// Taken from https://adhamhurani.blogspot.com/2010/08/c-how-to-add-distance-and-calculate-new.html
        /// </summary>
        /// <param name="source">Orginal Point</param>
        /// <param name="range">Range in meters</param>
        /// <param name="trueBearing">Bearing in degrees (Must be true and not magnetic)</param>
        /// <returns>End-point from the source given the desired range and bearing.</returns>
        public static Point CalculatePointFromSource(Point source, double range, double trueBearing)
        {
            var latA = source.Coordinate.Latitude * DegreesToRadians;
            var lonA = source.Coordinate.Longitude * DegreesToRadians;
            var angularDistance = range / EarthRadius;
            var trueCourse = trueBearing * DegreesToRadians;

            var lat = Math.Asin(Math.Sin(latA) * Math.Cos(angularDistance) + Math.Cos(latA) * Math.Sin(angularDistance) * Math.Cos(trueCourse));

            var dlon = Math.Atan2(Math.Sin(trueCourse) * Math.Sin(angularDistance) * Math.Cos(latA), Math.Cos(angularDistance) - Math.Sin(latA) * Math.Sin(lat));
            var lon = (lonA + dlon + Math.PI) % (Math.PI * 2) - Math.PI;

            return new Point(lat * RadiansToDegrees, lon * RadiansToDegrees);
        }

        public double DegreeToRadian(double angle) { return Math.PI * angle / 180.0; }
        public double RadianToDegree(double angle) { return 180.0 * angle / Math.PI; }

        public static double BearingTo(Coordinate source, Coordinate dest)
        {
            var lat1 = source.Latitude * DegreesToRadians;
            var lat2 = dest.Latitude * DegreesToRadians;
            var dLon = dest.Longitude * DegreesToRadians - source.Longitude * DegreesToRadians;

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            var brng = Math.Atan2(y, x);

            return (brng * RadiansToDegrees + 360) % 360;
        }

        // So... Thanks to DArt of LotATC we have learned that, on the Caucuses, things are "Whack".
        // The TRUE bearing, on the caucuses, in DCS is the same as the MAGNETIC bearing in real-life
        // so for things like bearings to match up correctly using haversine calculations we need to
        // convert the result to magnetic TWICE.
        public static double CaucasusHeadingFix(Point position, double trueBearing)
        {
            if (!IsCaucasus(position)) return trueBearing;
            var magneticBearing = trueBearing + CalculateOffset(position) - CaucasusFudgeFactor;
            Logger.Debug($"True Bearing: {trueBearing}, Caucasus 'true' heading {magneticBearing}");
            return magneticBearing;

        }

        public static double TrueToMagnetic(Point position, double trueBearing)
        {
            var magneticBearing = trueBearing - CalculateOffset(position);

            if (magneticBearing < 0)
            {
                magneticBearing += 360;
            }
            Logger.Debug($"True Bearing: {trueBearing}, Magnetic Bearing {magneticBearing}");
            return magneticBearing;
        }

        public static double MagneticToTrue(Point position, double trueBearing)
        {
            double magneticBearing = trueBearing + CalculateOffset(position);

            if (magneticBearing > 360)
            {
                magneticBearing -= 360;
            }
            Logger.Debug($"True Bearing: {trueBearing}, Magnetic Bearing {magneticBearing}");
            return magneticBearing;
        }

        private static double CalculateOffset(IPosition position)
        {
            var calculator = new WmmGeomagnetismCalculator(Spheroid.Wgs84);
            var result = calculator.TryCalculate(position, DateTime.UtcNow);
            return result.Declination;
        }

        public static bool IsCaucasus(Point position)
        {
            var isCaucasus = position.Coordinate.Latitude >= 39 && position.Coordinate.Latitude <= 48 && position.Coordinate.Longitude >= 27 && position.Coordinate.Longitude <= 48;
            Logger.Debug($"Position within Caucasus? {isCaucasus}");
            return isCaucasus;
        }
    }
}

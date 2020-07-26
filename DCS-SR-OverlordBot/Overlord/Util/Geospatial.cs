using NLog;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Util
{
    class Geospatial
    {
        // Fudge factor to try and bring the bot inline with what we are seeing in game.
        private static readonly double CAUCASUS_FUDGE_FACTOR = 1.5;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        const double EarthRadius = 6378137.0;
        const double DegreesToRadians = 0.0174532925;
        const double RadiansToDegrees = 57.2957795;
        /// <summary>
        /// Calculates the new-point from a given source at a given range (meters) and bearing (degrees).
        /// Taken from https://adhamhurani.blogspot.com/2010/08/c-how-to-add-distance-and-calculate-new.html
        /// </summary>
        /// <param name="source">Orginal Point</param>
        /// <param name="range">Range in meters</param>
        /// <param name="bearing">Bearing in degrees (Must be true and not magnetic)</param>
        /// <returns>End-point from the source given the desired range and bearing.</returns>
        public static Geo.Geometries.Point CalculatePointFromSource(Geo.Geometries.Point source, double range, double trueBearing)
        {
            double latA = source.Coordinate.Latitude * DegreesToRadians;
            double lonA = source.Coordinate.Longitude * DegreesToRadians;
            double angularDistance = range / EarthRadius;
            double trueCourse = trueBearing * DegreesToRadians;

            double lat = Math.Asin(Math.Sin(latA) * Math.Cos(angularDistance) + Math.Cos(latA) * Math.Sin(angularDistance) * Math.Cos(trueCourse));

            double dlon = Math.Atan2(Math.Sin(trueCourse) * Math.Sin(angularDistance) * Math.Cos(latA), Math.Cos(angularDistance) - Math.Sin(latA) * Math.Sin(lat));
            double lon = ((lonA + dlon + Math.PI) % (Math.PI * 2)) - Math.PI;

            return new Geo.Geometries.Point(lat * RadiansToDegrees, lon * RadiansToDegrees);
        }

        public double DegreeToRadian(double angle) { return Math.PI * angle / 180.0; }
        public double RadianToDegree(double angle) { return 180.0 * angle / Math.PI; }

        public static double BearingTo(Geo.Coordinate source, Geo.Coordinate dest)
        {
            double lat1 = source.Latitude * DegreesToRadians;
            double lat2 = dest.Latitude * DegreesToRadians;
            double dLon = (dest.Longitude * DegreesToRadians) - (source.Longitude * DegreesToRadians);

            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            double brng = Math.Atan2(y, x);

            return (brng * RadiansToDegrees + 360) % 360;
        }

        // So... Thanks to DArt of LotATC we have learned that, on the Caucuses, things are "Whack".
        // The TRUE bearing, on the caucuses, in DCS is the same as the MAGNETIC bearing in real-life
        // so for things like bearings to match up correctly using haversine calculations we need to
        // convert the result to magnetic TWICE.
        //
        // At some point we will need to flag this based on the position because this "twice" thing
        // only happens on Caucuses while other maps are real-world accurate.,
        public static double TrueToMagnetic(Geo.Geometries.Point position, double trueBearing)
        {
            double magneticBearing;
            if (IsCaucasus(position))
            {
                magneticBearing = trueBearing - ((2 * CalculateOffset(position)) - CAUCASUS_FUDGE_FACTOR);
            }
            else
            {
                magneticBearing = trueBearing - CalculateOffset(position);
            }

            if (magneticBearing < 0)
            {
                magneticBearing += 360;
            }
            Logger.Debug($"True Bearing: {trueBearing}, Magnetic Bearing {magneticBearing}");
            return magneticBearing;
        }

        public static double MagneticToTrue(Geo.Geometries.Point position, double trueBearing)
        {
            double magneticBearing;
            if (IsCaucasus(position))
            {
                magneticBearing = trueBearing + ((2 * CalculateOffset(position)) - CAUCASUS_FUDGE_FACTOR);
            }
            else
            {
                magneticBearing = trueBearing + CalculateOffset(position);
            }
            if (magneticBearing > 360)
            {
                magneticBearing -= 360;
            }
            Logger.Debug($"True Bearing: {trueBearing}, Magnetic Bearing {magneticBearing}");
            return magneticBearing;
        }

        private static double CalculateOffset(Geo.Geometries.Point position)
        {
            var calculator = new Geo.Geomagnetism.WmmGeomagnetismCalculator(Geo.Geodesy.Spheroid.Wgs84);
            var result = calculator.TryCalculate(position, DateTime.UtcNow);
            return result.Declination;
        }

        private static bool IsCaucasus(Geo.Geometries.Point position)
        {
            bool isCaucasus = position.Coordinate.Latitude >= 27 && position.Coordinate.Latitude <= 47 && position.Coordinate.Longitude >= 39 && position.Coordinate.Longitude <= 48;
            Logger.Debug($"Position within Caucasus? {isCaucasus}");
            return isCaucasus;
        }
    }
}

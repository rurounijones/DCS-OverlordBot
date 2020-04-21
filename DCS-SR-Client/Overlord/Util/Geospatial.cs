using NetTopologySuite.Geometries;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public static Point CalculatePointFromSource(Point source, double range, double trueBearing)
        {
            // Our Point class with PostGIS is lon/lat format so X is lon and Y is lat... God this is confusing.
            // Why can't they just use the names lat/lon instead of this 50/50 might-be-right stuff.
            double latA = source.Y * DegreesToRadians;
            double lonA = source.X * DegreesToRadians;
            double angularDistance = range / EarthRadius;
            double trueCourse = trueBearing * DegreesToRadians;

            double lat = Math.Asin(Math.Sin(latA) * Math.Cos(angularDistance) + Math.Cos(latA) * Math.Sin(angularDistance) * Math.Cos(trueCourse));

            double dlon = Math.Atan2(Math.Sin(trueCourse) * Math.Sin(angularDistance) * Math.Cos(latA), Math.Cos(angularDistance) - Math.Sin(latA) * Math.Sin(lat));
            double lon = ((lonA + dlon + Math.PI) % (Math.PI * 2)) - Math.PI;

            return new Point(lon * RadiansToDegrees, lat * RadiansToDegrees);
        }


        class GeoPoint : Geo.Abstractions.Interfaces.IPosition
        {
            private readonly Point Position;
            public GeoPoint(Point position)
            {
                Position = position;
            }

            public Geo.Coordinate GetCoordinate()
            {
                // Remember Point is Lon/Lat but Coordinate is Lat/lon so flip em
                return new Geo.Coordinate(Position.Coordinate.Y, Position.Coordinate.X);
            }
        }

        // So... Thanks to DArt of LotATC we have learned that, on the Caucuses, things are "Whack".
        // The TRUE bearing, on the caucuses, in DCS is the same as the MAGNETIC bearing in real-life
        // so for things like bearings to match up correctly using haversine calculations we need to
        // convert the result to magnetic TWICE.
        //
        // At some point we will need to flag this based on the position because this "twice" thing
        // only happens on Caucuses while other maps are real-world accurate.,
        public static double TrueToMagnetic(Point position, double trueBearing)
        {
            double magneticBearing;
            if (IsCaucasus(position)) {
                magneticBearing = trueBearing - ((2 * CalculateOffset(position)) - CAUCASUS_FUDGE_FACTOR);
            }
            else
            {
                magneticBearing = trueBearing - CalculateOffset(position);
            }

            if(magneticBearing < 0)
            {
                magneticBearing += 360;
            }
            Logger.Debug($"True Bearing: {trueBearing}, Magnetic Bearing {magneticBearing}");
            return magneticBearing;
        }

        public static double MagneticToTrue(Point position, double trueBearing)
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

        private static double CalculateOffset(Point position)
        {
            var geopoint = new GeoPoint(position);
            var calculator = new Geo.Geomagnetism.WmmGeomagnetismCalculator(Geo.Geodesy.Spheroid.Wgs84);
            var result = calculator.TryCalculate(geopoint, DateTime.UtcNow);
            return result.Declination;
        }

        private static bool IsCaucasus(Point position)
        {
            // Remember, point is lon lat so X is lon
            bool isCaucasus = position.Y >= 39 && position.Y <= 48 && position.X >= 27 && position.X <= 47;
            Logger.Debug($"Position within Caucasus? {isCaucasus}");
            return isCaucasus;

        }
    }
}

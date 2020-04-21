using Newtonsoft.Json;
using Geo.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    /// <summary>
    /// Class that holds the information needed for a navigation point. This means a decimal lat/lon pair
    /// with a radius (in meters) that can be used to determine if a unit is within the point or not.
    /// </summary>
    public class NavigationPoint
    {
        /// <summary>
        /// Name of the Navigation Point.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Decimal latitude (e.g. 41.12324)
        /// </summary>
        [JsonProperty(PropertyName = "lat")]
        public double Latitude { get; set; }

        /// <summary>
        /// Decimal Longitude (e.g. 37.12324)
        /// </summary>
        [JsonProperty(PropertyName = "lon")]
        public double Longitude { get; set; }

        /// <summary>
        /// Altitude in Meters
        /// </summary>
        public double Altitude { get; set; }

        /// <summary>
        /// Radius of the navigation point from the central lat/lon
        /// Used to determine if a unit is within the navigation point or not
        /// </summary>
        public int Radius { get; set; }

        public Circle Position
        {
            get
            {
                return new Circle(Latitude, Longitude, Altitude, Radius);
            }
        }
    }
}

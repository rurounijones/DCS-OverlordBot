using Geo;
using Newtonsoft.Json;
using System;

namespace RurouniJones.DCS.Airfields.Structure
{
    public class TaxiPoint
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "lat")]
        public double Latitude { get; set; }

        [JsonProperty(PropertyName = "lon")]
        public double Longitude { get; set; }

        private Coordinate Coordinate
        {
            get
            {
                return new Coordinate(Latitude, Longitude);
            }
        }

        public double DistanceTo(Coordinate otherCoordinate)
        {
            var baseRad = Math.PI * Coordinate.Latitude / 180;
            var targetRad = Math.PI * otherCoordinate.Latitude / 180;
            var theta = Coordinate.Longitude - otherCoordinate.Longitude;
            var thetaRad = Math.PI * theta / 180;

            double dist =
                Math.Sin(baseRad) * Math.Sin(targetRad) + Math.Cos(baseRad) *
                Math.Cos(targetRad) * Math.Cos(thetaRad);
            dist = Math.Acos(dist);

            dist = dist * 180 / Math.PI;
            return dist * 60 * 1.1515;
        }

        override public string ToString()
        {
            return $"{Name}: Lat {Latitude} / Lon {Longitude}";

        }
    }
}

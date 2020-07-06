using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RurouniJones.DCS.Airfields.Structure
{
    public class ParkingSpot : TaxiPoint
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        [JsonProperty(PropertyName = "points")]
        public List<BoundaryPoint> BoundaryPoints { get; set; }

        public Geo.Geometries.Polygon Area { get; set; }

        [OnDeserialized]
        internal void BuildParkingSpotPolygon(StreamingContext context)
        {
            try
            {
                if (BoundaryPoints.Count > 0)
                {
                    List<Geo.Coordinate> points = new List<Geo.Coordinate>();

                    foreach (var boundaryPoint in BoundaryPoints)
                    {
                        points.Add(boundaryPoint.Coordinate);
                    }
                    points.Add(points[0]);
                    Area = new Geo.Geometries.Polygon(points.ToArray());
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error building polygon of Parking Spot");
            }
        }

        public class BoundaryPoint
        {
            [JsonProperty(PropertyName = "lat")]
            public double Latitude { get; set; }

            [JsonProperty(PropertyName = "lon")]
            public double Longitude { get; set; }

            public Geo.Coordinate Coordinate
            {
                get
                {
                    return new Geo.Coordinate(Latitude, Longitude);
                }
            }
        }
    }
}

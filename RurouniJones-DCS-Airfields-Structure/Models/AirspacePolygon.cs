using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation
{
    class AirspacePolygon
    {
        public string Name { get; set; }

        [JsonProperty(PropertyName = "points")]
        public List<BoundaryPoint> BoundaryPoints { get; set; }

        private Geo.Geometries.Polygon _area;
        public Geo.Geometries.Polygon Area
        {
            get
            {
                if (_area != null)
                {
                    return _area;
                }

                List<Geo.Coordinate> points = new List<Geo.Coordinate>();

                foreach (var boundaryPoint in BoundaryPoints)
                {
                    points.Add(boundaryPoint.Coordinate);
                }
                points.Add(points[0]);
                _area = new Geo.Geometries.Polygon(points.ToArray());
                return _area;
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

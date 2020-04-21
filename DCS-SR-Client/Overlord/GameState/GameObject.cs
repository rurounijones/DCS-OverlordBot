using NetTopologySuite.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    partial class GameState
    {
        public class GameObject
        {
            public string Id { get; set; }
            public Point Position { get; set; }

            public double Altitude { get; set; }

            public string Pilot { get; set; }
            public int Coalition { get; set; }

            public Geo.Geometries.Point GeoPoint
            {
                get
                {
                    return new Geo.Geometries.Point(Position.Y, Position.X, Altitude);
                }
            }
}
    }
}

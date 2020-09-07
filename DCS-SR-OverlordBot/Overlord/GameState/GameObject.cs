using Geo.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    public class GameObject
    {
        public string Id { get; set; }
        public Point Position { get; set; }

        public string Pilot { get; set; }
        public Coalition Coalition { get; set; }
        public double Altitude { get; set; }
        public int? Heading { get; set; }
        public string Name { get; set; }
    }
}

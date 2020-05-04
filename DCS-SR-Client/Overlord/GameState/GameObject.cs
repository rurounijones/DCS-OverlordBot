namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    public class GameObject
    {
        public string Id { get; set; }
        public Geo.Geometries.Point Position { get; set; }

        public string Pilot { get; set; }
        public int Coalition { get; set; }
        public double Altitude { get; set; }
        public int? Heading { get; set; }
        public string Name { get; set; }
    }
}

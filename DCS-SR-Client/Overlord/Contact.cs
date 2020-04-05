namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    public class Contact {
        public string Id { get; set; }
        public int Bearing { get; set; }
        public int Range { get; set; }
        public int Altitude { get; set; }
        public int? Heading { get; set; }
        public string Name { get; set; }
    }
}

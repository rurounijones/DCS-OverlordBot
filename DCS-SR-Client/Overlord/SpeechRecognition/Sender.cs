namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{

    class Sender
    {
        public string Group { get; }
        public int Flight { get; }
        public int Plane { get; }

        public Sender(string group, int flight, int plane)
        {
            this.Group = group;
            this.Flight = flight;
            this.Plane = plane;
        }

        public override string ToString()
        {
            return $"{Group} {Flight} {Plane}";
        }
    }
}


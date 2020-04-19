using static Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using NetTopologySuite.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{

    class Sender
    {
        public string Group { get; }
        public int Flight { get; }
        public int Plane { get; }
        public GameObject GameObject { get; set; }

        public Point Position {
            get {
                return GameObject.Position;
            }
        }

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


using NetTopologySuite.Geometries;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    partial class GameState
    {
        public class GameObject
        {
            public string Id { get; set; }
            public Point Position { get; set; }

            public string Pilot { get; set; }
            public int Coalition { get; set; }
        }

    }
}

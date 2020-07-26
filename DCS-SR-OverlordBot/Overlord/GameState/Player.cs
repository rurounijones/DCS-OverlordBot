namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    public class Player : GameObject
    {
        public string Group { get; set; }
        public int Flight { get; set; }
        public int Plane { get; set; }

        public string Callsign
        {
            get
            {
                return $"{Group} {Flight} {Plane}";
            }
        }
    }
}

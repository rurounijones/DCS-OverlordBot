using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    public class DCSPlayerSideInfo
    {
        public string name = "";
        public int side = 0;
        public DCSLatLngPosition LngLngPosition { get; set; } = new DCSLatLngPosition();

        public DcsPosition Position { get; set; } = new DcsPosition();

        public void Reset()
        {
            name = "";
            side = 0;
            LngLngPosition = new DCSLatLngPosition();
            Position = new DcsPosition();
        }
    }
}
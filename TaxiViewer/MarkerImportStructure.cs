using RurouniJones.DCS.Airfields.Structure;

namespace TaxiViewer.MarkerImport
{
    public class Rootobject
    {
        public Savedpoint[] savedPoints { get; set; }
    }

    public class Savedpoint
    {
        public double lat { get; set; }
        public double lon { get; set; }
        public string name { get; set; }

        public override string ToString()
        {
            return($"{name ?? "(unnamed)"} @ {lat.ToString()} { lon.ToString()}");
        }

        public NavigationPoint navpoint = null;

    }

}
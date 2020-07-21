using System.Collections.Generic;

namespace RurouniJones.DCS.Airfields.Controllers
{
    public class TaxiInstructions
    {
        public string DestinationName { get; set; }

        public List<string> TaxiwayNames { get; set; } = new List<string>();

        public List<string> Comments { get; set; } = new List<string>();

        public override string ToString() {
            string val = $"{DestinationName}, {string.Join("-", TaxiwayNames)}";
            if (Comments.Count > 0) {
                val += $" , { string.Join(" ", Comments)} ";
            }
            return val;
        }
    }
}

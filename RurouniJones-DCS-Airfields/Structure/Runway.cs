using Newtonsoft.Json;

namespace RurouniJones.DCS.Airfields.Structure
{
    public class Runway : TaxiPoint
    {
        [JsonProperty(PropertyName = "heading")]
        public int Heading { get; set; }
    }
}

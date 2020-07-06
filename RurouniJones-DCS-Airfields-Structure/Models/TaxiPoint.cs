using Newtonsoft.Json;

namespace RurouniJones.DCS.Airfields.Structure
{
    public class TaxiPoint
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}

using Newtonsoft.Json;

namespace RurouniJones.DCS.Airfields.Structure
{
    public class NavigationPath
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "source")]
        public string Source { get; set; }

        [JsonProperty(PropertyName = "target")]
        public string Target { get; set; }

        [JsonProperty(PropertyName = "cost")]
        public int Cost { get; set; }
    }
}

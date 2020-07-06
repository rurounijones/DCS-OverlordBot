using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation
{
    public class TaxiPath
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

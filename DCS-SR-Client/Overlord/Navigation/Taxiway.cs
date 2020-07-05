using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation
{
    public class Taxiway
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "connections")]
        public List<string> Connections { get; set; }

        [JsonProperty(PropertyName = "cost")]
        public int Cost { get; set; }
    }
}

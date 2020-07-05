using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation
{
    public class TaxiPoint
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}

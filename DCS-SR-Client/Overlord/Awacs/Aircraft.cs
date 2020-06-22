using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Awacs
{
    public class Aircraft
    {
        [JsonProperty(PropertyName = "dcs_id")]
        public string DcsId { get; set; }

        [JsonProperty(PropertyName = "nato_name")]
        public string NatoName { get; set; }

    }
}

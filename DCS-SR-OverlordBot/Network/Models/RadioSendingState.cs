using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class RadioSendingState
    {
        [JsonIgnore]
        public long LastSentAt { get; set; }

        public bool IsSending { get; set; }

        public int SendingOn { get; set; }
    }
}
using Newtonsoft.Json;

namespace RurouniJones.DCS.OverlordBot.Network
{
    public class RadioSendingState
    {
        [JsonIgnore]
        public long LastSentAt { get; set; }

        public bool IsSending { get; set; }

        public int SendingOn { get; set; }
    }
}
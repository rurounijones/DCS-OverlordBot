using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.VAICOM.Models
{
    public class VAICOMMessageWrapper
    {
        public int MessageType; //1 is InhibitTX
        public bool InhibitTX;
        
        [JsonIgnore] 
        public long LastReceivedAt;

    }
}

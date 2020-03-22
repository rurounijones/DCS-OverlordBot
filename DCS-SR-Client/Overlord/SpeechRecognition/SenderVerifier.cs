using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    class SenderVerifier
    {
        [Trace]
        public static async Task<bool> Verify(Sender sender)
        {
            if (await GameState.DoesPilotExist(sender.Group, sender.Flight, sender.Plane) != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}

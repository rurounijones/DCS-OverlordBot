using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    class SenderVerifier
    {
        public static async Task<bool> Verify(Sender sender)
        {
            if (await GameState.DoesPilotExist(sender.Group, sender.Flight, sender.Plane) == true)
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

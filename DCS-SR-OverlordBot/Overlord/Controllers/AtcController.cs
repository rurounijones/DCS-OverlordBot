using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using RurouniJones.DCS.Airfields;
using System.Collections.Concurrent;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class AtcController : AbstractController
    {

        protected override string None(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string Unknown(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "I could not understand your transmission";
        }

        protected override string RadioCheck(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + $" ground, five-by-five";
        }

        protected override string BogeyDope(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string BearingToAirbase(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string BearingToFriendlyPlayer(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string Declare(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string Picture(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string SetWarningRadius(BaseRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string ReadyToTaxi(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "ground, " + Intents.ReadytoTaxi.Process(radioCall).Result;
        }

        protected override string NullSender(BaseRadioCall _)
        {
            return "Last transmitter, I could not recognise your call-sign";
        }

        protected override string InboundToAirbase(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "copy inbound.";
        }

        protected override string UnverifiedSender(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "I cannot find you on scope.";
        }

        protected override bool IsAddressedToController(BaseRadioCall radioCall)
        {
            // TODO, make this return false unless a known airbase name has been used.
            return true;
        }

        private string ResponsePrefix(BaseRadioCall radioCall)
        {
            string name;
            if (Populator.Airfields.Where(airfield => airfield.Name.Equals(radioCall.AirbaseName)).ToList().Count > 0)
            {
                name = AirbasePronouncer.PronounceAirbase(radioCall.AirbaseName);
            }
            else
            {
                name = "ATC";
            }

            return $"{radioCall.Sender.Callsign}, {name}, ";
        }
    }
}

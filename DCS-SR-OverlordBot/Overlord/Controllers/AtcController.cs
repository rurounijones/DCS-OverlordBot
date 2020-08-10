using static Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Constants;

using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using System.Collections.Concurrent;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class AtcController : AbstractController
    {
        protected override string None(IRadioCall radioCall)
        {
            return null;
        }

        protected override string Unknown(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + ", I could not understand your transmission";
        }

        protected override string RadioCheck(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "ground, five-by-five";
        }

        protected override string BogeyDope(IRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string BearingToAirbase(IRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string BearingToFriendlyPlayer(IRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string Declare(IRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string Picture(IRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string SetWarningRadius(IRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        protected override string ReadyToTaxi(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "ground, " + Intents.ReadytoTaxi.Process(radioCall).Result;
        }

        protected override string NullSender(IRadioCall _)
        {
            return "Last transmitter, I could not recognize your call-sign";
        }

        protected override string InboundToAirbase(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "tower , copy inbound.";
        }

        protected override string UnverifiedSender(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + ", I cannot find you on scope.";
        }

        protected override bool IsAddressedToController(IRadioCall radioCall)
        {
            // TODO, make this return false unless a known airbase name has been used.
            return true;
        }

        private static string ResponsePrefix(IRadioCall radioCall)
        {
            var name = Airfields.Where(airfield => airfield.Name.Equals(radioCall.AirbaseName)).ToList().Count > 0 ? AirbasePronouncer.PronounceAirbase(radioCall.AirbaseName) : "ATC";
            return $"{radioCall.Sender.Callsign}, {name} ";
        }
    }
}

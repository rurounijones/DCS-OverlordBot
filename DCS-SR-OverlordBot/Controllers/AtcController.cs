using System.Collections.Concurrent;
using System.Linq;
using RurouniJones.DCS.OverlordBot.Intents;
using RurouniJones.DCS.OverlordBot.RadioCalls;
using RurouniJones.DCS.OverlordBot.SpeechOutput;

namespace RurouniJones.DCS.OverlordBot.Controllers
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
            return ResponsePrefix(radioCall) + "ground, " + ReadytoTaxi.Process(radioCall).Result;
        }

        protected override string NullSender(IRadioCall _)
        {
            return "Last transmitter, I could not recognize your call-sign";
        }

        protected override string InboundToAirbase(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + " tower, copy inbound.";
        }

        protected override string UnverifiedSender(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + ", I cannot find you on scope.";
        }

        protected override bool IsAddressedToController(IRadioCall radioCall)
        {
            return Constants.Airfields.Any(airfield => airfield.Name.Equals(radioCall.AirbaseName));
        }

        private static string ResponsePrefix(IRadioCall radioCall)
        {
            var name = Constants.Airfields.Any(airfield => airfield.Name.Equals(radioCall.AirbaseName)) ? AirbasePronouncer.PronounceAirbase(radioCall.AirbaseName) : "ATC";
            return $"{radioCall.Sender.Callsign}, {name} ";
        }
    }
}

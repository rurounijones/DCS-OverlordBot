using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using RurouniJones.DCS.Airfields;
using System.Collections.Concurrent;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class AtcController : AbstractController
    {

        public override string None(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string Unknown(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "I could not understand your transmission";
        }

        public override string RadioCheck(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + $" ground, five-by-five";
        }

        public override string BogeyDope(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        public override string BearingToAirbase(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        public override string BearingToFriendlyPlayer(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        public override string Declare(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        public override string Picture(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        public override string SetWarningRadius(BaseRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            return $"{radioCall.Sender.Callsign}, This is an ATC frequency";
        }

        public override string ReadyToTaxi(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "ground, " + Intents.ReadytoTaxi.Process(radioCall).Result;
        }

        public override string NullSender(BaseRadioCall _)
        {
            return "Last transmitter, I could not recognise your call-sign";
        }

        public override string UnverifiedSender(BaseRadioCall radioCall)
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
                name = Intents.BearingToAirbase.PronounceAirbase(radioCall.AirbaseName);
            }
            else
            {
                name = "ATC";
            }

            return $"{radioCall.Sender.Callsign}, {name}, ";
        }
    }
}

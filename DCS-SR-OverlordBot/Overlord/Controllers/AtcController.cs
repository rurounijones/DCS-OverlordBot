using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using RurouniJones.DCS.Airfields;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    class AtcController : AbstractController
    {

        public override string None(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string RadioCheck(BaseRadioCall radioCall)
        {
            return $"{radioCall.Sender.Callsign}, ATC Ground, five-by-five";
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
            string name;
            if (Populator.Airfields.Where(airfield => airfield.Name.Equals(radioCall.ReceiverName)).ToList().Count > 0)
            {
                name = Intents.BearingToAirbase.PronounceAirbase(radioCall.ReceiverName);
            } else
            {
                name = "ATC";
            }
            return $"{radioCall.Sender.Callsign}, {name} ground, " + Intents.ReadytoTaxi.Process(radioCall).Result;
        }

        protected override bool IsAddressedToController(BaseRadioCall radioCall)
        {
            // TODO, make this return false unless a known airbase name has been used.
            return true;
        }
    }
}

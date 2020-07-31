using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using System.Collections.Concurrent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class MuteController : AbstractController
    {
        public override string None(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string Unknown(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string RadioCheck(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string BogeyDope(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string BearingToAirbase(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string BearingToFriendlyPlayer(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string Declare(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string Picture(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string SetWarningRadius(BaseRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            return null;
        }

        public override string ReadyToTaxi(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string InboundToAirbase(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string NullSender(BaseRadioCall radioCall)
        {
            return null;
        }

        public override string UnverifiedSender(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override bool IsAddressedToController(BaseRadioCall radioCall)
        {
            return false;
        }
    }
}

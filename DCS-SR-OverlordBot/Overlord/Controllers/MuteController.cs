using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using System.Collections.Concurrent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class MuteController : AbstractController
    {
        protected override string None(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string Unknown(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string RadioCheck(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string BogeyDope(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string BearingToAirbase(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string BearingToFriendlyPlayer(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string Declare(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string Picture(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string SetWarningRadius(BaseRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            return null;
        }

        protected override string ReadyToTaxi(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string InboundToAirbase(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string NullSender(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string UnverifiedSender(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override bool IsAddressedToController(BaseRadioCall radioCall)
        {
            return false;
        }
    }
}

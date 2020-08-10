using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using System.Collections.Concurrent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class MuteController : AbstractController
    {
        protected override string None(IRadioCall radioCall)
        {
            return null;
        }

        protected override string Unknown(IRadioCall radioCall)
        {
            return null;
        }

        protected override string RadioCheck(IRadioCall radioCall)
        {
            return null;
        }

        protected override string BogeyDope(IRadioCall radioCall)
        {
            return null;
        }

        protected override string BearingToAirbase(IRadioCall radioCall)
        {
            return null;
        }

        protected override string BearingToFriendlyPlayer(IRadioCall radioCall)
        {
            return null;
        }

        protected override string Declare(IRadioCall radioCall)
        {
            return null;
        }

        protected override string Picture(IRadioCall radioCall)
        {
            return null;
        }

        protected override string SetWarningRadius(IRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            return null;
        }

        protected override string ReadyToTaxi(IRadioCall radioCall)
        {
            return null;
        }

        protected override string InboundToAirbase(IRadioCall radioCall)
        {
            return null;
        }

        protected override string NullSender(IRadioCall radioCall)
        {
            return null;
        }

        protected override string UnverifiedSender(IRadioCall radioCall)
        {
            return null;
        }

        protected override bool IsAddressedToController(IRadioCall radioCall)
        {
            return false;
        }
    }
}

using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public abstract class AbstractController
    {
        /// <summary>
        /// The callsign that this controller answers to. If null then it will answer to all callsigns deemed
        /// valid by the LUIS application.
        /// </summary>
        public string Callsign { get; set; }

        public abstract string NullSender(BaseRadioCall radioCall);

        public abstract string UnverifiedSender(BaseRadioCall radioCall);

        public abstract string None(BaseRadioCall radioCall);

        public abstract string Unknown(BaseRadioCall radioCall);

        public abstract string RadioCheck(BaseRadioCall radioCall);

        public abstract string BogeyDope(BaseRadioCall radioCall);

        public abstract string BearingToAirbase(BaseRadioCall radioCall);

        public abstract string BearingToFriendlyPlayer(BaseRadioCall radioCall);

        public abstract string Declare(BaseRadioCall radioCall);

        public abstract string Picture(BaseRadioCall radioCall);

        public abstract string SetWarningRadius(BaseRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue);

        public abstract string ReadyToTaxi(BaseRadioCall radioCall);

        protected abstract bool IsAddressedToController(BaseRadioCall radioCall);

    }
}

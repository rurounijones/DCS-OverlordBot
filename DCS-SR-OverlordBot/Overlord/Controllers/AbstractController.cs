using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
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

        /// <summary>
        /// The voice that the controller users to speak. 
        /// <see cref="https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support#standard-voices"/>
        /// </summary>
        public string Voice { get; set; }

        /// <summary>
        /// The radio the controller is listening to.
        /// </summary>
        public RadioInformation Radio { get; set; }

        public string ProcessRadioCall(BaseRadioCall radioCall)
        {
            if (radioCall.Intent == "None")
                return Task.Run(() => None(radioCall)).Result;

            if (radioCall.Sender == null)
                return Task.Run(() => NullSender(radioCall)).Result;

            if (!Task.Run(() => GameQuerier.GetPilotData(radioCall)).Result)
                return Task.Run(() => UnverifiedSender(radioCall)).Result;

            switch (radioCall.Intent)
            {
                case "RadioCheck":
                    return Task.Run(() => RadioCheck(radioCall)).Result;
                case "BogeyDope":
                    return Task.Run(() => BogeyDope(radioCall)).Result;
                case "BearingToAirbase":
                    return Task.Run(() => BearingToAirbase(radioCall)).Result;
                case "BearingToFriendlyPlayer":
                    return Task.Run(() => BearingToFriendlyPlayer(radioCall)).Result;
                case "SetWarningRadius":
                    return Task.Run(() => SetWarningRadius(radioCall, Voice, Radio.TransmissionQueue)).Result;
                case "Picture":
                    return Task.Run(() => Picture(radioCall)).Result;
                case "Declare":
                    return Task.Run(() => Declare(radioCall)).Result;
                case "ReadyToTaxi":
                    return Task.Run(() => ReadyToTaxi(radioCall)).Result;
                case "InboundToAirbase":
                    return Task.Run(() => InboundToAirbase(radioCall)).Result;
                default:
                    return Task.Run(() => Unknown(radioCall)).Result;
            };
        }

        protected abstract string NullSender(BaseRadioCall radioCall);

        protected abstract string UnverifiedSender(BaseRadioCall radioCall);

        protected abstract string None(BaseRadioCall radioCall);

        protected abstract string Unknown(BaseRadioCall radioCall);

        protected abstract string RadioCheck(BaseRadioCall radioCall);

        protected abstract string BogeyDope(BaseRadioCall radioCall);

        protected abstract string BearingToAirbase(BaseRadioCall radioCall);

        protected abstract string BearingToFriendlyPlayer(BaseRadioCall radioCall);

        protected abstract string Declare(BaseRadioCall radioCall);

        protected abstract string Picture(BaseRadioCall radioCall);

        protected abstract string SetWarningRadius(BaseRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue);

        protected abstract string ReadyToTaxi(BaseRadioCall radioCall);

        protected abstract string InboundToAirbase(BaseRadioCall radioCall);

        protected abstract bool IsAddressedToController(BaseRadioCall radioCall);

    }
}

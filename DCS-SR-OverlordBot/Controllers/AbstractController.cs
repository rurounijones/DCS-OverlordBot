using System.Collections.Concurrent;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using RurouniJones.DCS.OverlordBot.RadioCalls;

namespace RurouniJones.DCS.OverlordBot.Controllers
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

        public string ProcessRadioCall(IRadioCall radioCall)
        {
            using (var activity = Constants.ActivitySource.StartActivity("Controller.ProcessRadioCall"))
            {
                if (radioCall.Intent == "None")
                    return Task.Run(() => None(radioCall)).Result;

                if (string.IsNullOrEmpty(radioCall.ReceiverName))
                    return Task.Run(() => None(radioCall)).Result;

                if (radioCall.Sender == null)
                {
                    activity?.AddTag("Response", "Not Recognized");
                    return Task.Run(() => NullSender(radioCall)).Result;
                }

                if (radioCall.Sender.Id == null)
                {
                    activity?.AddTag("Response", "Not On Scope");
                    return Task.Run(() => UnverifiedSender(radioCall)).Result;
                }

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
                        return Task.Run(() => ReadyToTaxi(radioCall, Voice, Radio.TransmissionQueue)).Result;
                    case "InboundToAirbase":
                        return Task.Run(() => InboundToAirbase(radioCall, Voice, Radio.TransmissionQueue)).Result;
                    default:
                        return Task.Run(() => Unknown(radioCall)).Result;
                }
            }
        }

        protected abstract string NullSender(IRadioCall radioCall);

        protected abstract string UnverifiedSender(IRadioCall radioCall);

        protected abstract string None(IRadioCall radioCall);

        protected abstract string Unknown(IRadioCall radioCall);

        protected abstract string RadioCheck(IRadioCall radioCall);

        protected abstract string BogeyDope(IRadioCall radioCall);

        protected abstract string BearingToAirbase(IRadioCall radioCall);

        protected abstract string BearingToFriendlyPlayer(IRadioCall radioCall);

        protected abstract string Declare(IRadioCall radioCall);

        protected abstract string Picture(IRadioCall radioCall);

        protected abstract string SetWarningRadius(IRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue);

        protected abstract string ReadyToTaxi(IRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue);

        protected abstract string InboundToAirbase(IRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue);

        protected abstract bool IsAddressedToController(IRadioCall radioCall);

    }
}

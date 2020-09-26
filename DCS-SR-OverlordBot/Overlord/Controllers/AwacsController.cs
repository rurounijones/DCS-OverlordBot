using System.Collections.Concurrent;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class AwacsController : AbstractController
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string DefaultCallsign = "Overlord"; 

        protected override string None(IRadioCall radioCall)
        {
            return null;
        }

        protected override string Unknown(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "I could not understand your transmission.";
        }

        protected override string RadioCheck(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "five-by-five.";
        }

        protected override string BogeyDope(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.BogeyDope.Process(radioCall).Result;
        }

        protected override string BearingToAirbase(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.BearingToAirbase.Process(radioCall).Result;
        }

        protected override string BearingToFriendlyPlayer(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.BearingToFriendlyPlayer.Process(new BearingToFriendlyPlayerRadioCall(radioCall)).Result;

        }

        protected override string Declare(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + "we do not support declare calls.";

        }

        protected override string Picture(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "we do not support picture calls.";
        }

        protected override string SetWarningRadius(IRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.SetWarningRadius.Process(new SetWarningRadiusRadioCall(radioCall), voice, responseQueue).Result;
        }

        protected override string ReadyToTaxi(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "this is an AWACS frequency.";
        }

        protected override string InboundToAirbase(IRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "this is an AWACS frequency.";
        }

        protected override string NullSender(IRadioCall radioCall)
        {
            return !IsAddressedToController(radioCall) ? null : "Last transmitter, I could not recognize your call-sign.";
        }

        protected override string UnverifiedSender(IRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "I cannot find you on scope.";
        }

        protected override bool IsAddressedToController(IRadioCall radioCall)
        {
            Logger.Debug($"Defined Callsign is {Callsign}, Received Callsign is {radioCall.AwacsCallsign}");

            if (string.IsNullOrEmpty(radioCall.AwacsCallsign))
                return false;

            if (string.IsNullOrEmpty(Callsign)) 
                return true;

            var result = string.IsNullOrEmpty(Callsign) != true && (radioCall.ReceiverName.ToLower() == "anyface" || radioCall.ReceiverName.ToLower().Equals(Callsign.ToLower()));

            return result;
        }

        private string ResponsePrefix(IRadioCall radioCall)
        {
            string responseCallsign;
            if (string.IsNullOrEmpty(Callsign) != true)
            {
                responseCallsign = Callsign;
            }
            else if (radioCall.AwacsCallsign != null && radioCall.AwacsCallsign.ToLower().Equals("anyface"))
            {
                responseCallsign = DefaultCallsign; // Overlord is the default callsign;
            }
            else if (string.IsNullOrEmpty(Callsign) && string.IsNullOrEmpty(radioCall.AwacsCallsign))
            {
                responseCallsign = DefaultCallsign; // Overlord is the default callsign;
            }
            else
            {
                responseCallsign = radioCall.AwacsCallsign;
            }
            return $"{radioCall.Sender.Callsign}, {responseCallsign}, ";
        }
    }
}

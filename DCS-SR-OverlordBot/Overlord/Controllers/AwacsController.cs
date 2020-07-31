using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using NLog;
using System.Collections.Concurrent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    public class AwacsController : AbstractController
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected override string None(BaseRadioCall radioCall)
        {
            return null;
        }

        protected override string Unknown(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "I could not understand your transmission.";
        }

        protected override string RadioCheck(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "five-by-five.";
        }

        protected override string BogeyDope(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.BogeyDope.Process(radioCall).Result;
        }

        protected override string BearingToAirbase(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.BearingToAirbase.Process(radioCall).Result;
        }

        protected override string BearingToFriendlyPlayer(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.BearingToFriendlyPlayer.Process(new BearingToFriendlyPlayerRadioCall(radioCall)).Result;

        }

        protected override string Declare(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.Declare.Process(new DeclareRadioCall(radioCall)).Result;

        }

        protected override string Picture(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "we do not support picture calls.";
        }

        protected override string SetWarningRadius(BaseRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            if (!IsAddressedToController(radioCall) || radioCall.Sender.Coalition == Coalition.Neutral)
                return null;
            return ResponsePrefix(radioCall) + Intents.SetWarningRadius.Process(new SetWarningRadiusRadioCall(radioCall), voice, responseQueue).Result;
        }

        protected override string ReadyToTaxi(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "this is an AWACS frequency.";
        }

        protected override string InboundToAirbase(BaseRadioCall radioCall)
        {
            return ResponsePrefix(radioCall) + "this is an AWACS frequency.";
        }

        protected override string NullSender(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return "Last transmitter, I could not recognise your call-sign.";
        }

        protected override string UnverifiedSender(BaseRadioCall radioCall)
        {
            if (!IsAddressedToController(radioCall))
                return null;
            return ResponsePrefix(radioCall) + "I cannot find you on scope.";
        }

        protected override bool IsAddressedToController(BaseRadioCall radioCall)
        {
            Logger.Debug($"Callsign is {Callsign}, Receiver is {radioCall.ReceiverName}");
            if (string.IsNullOrEmpty(Callsign) == true)
            {
                return true;
            }

            bool result = string.IsNullOrEmpty(Callsign) != true && (radioCall.ReceiverName.ToLower() == "anyface" || radioCall.ReceiverName.ToLower() == Callsign.ToLower());
            Logger.Debug($"Addressed to Controller is {result}");

            return result;
        }

        private string ResponsePrefix(BaseRadioCall radioCall)
        {
            string responseCallsign;
            if (string.IsNullOrEmpty(Callsign) != true)
            {
                responseCallsign = Callsign;
            }
            else if (radioCall.AwacsCallsign != null && radioCall.AwacsCallsign.ToLower().Equals("anyface"))
            {
                responseCallsign = "Overlord"; // Overlord is the default callsign;
            }
            else if (radioCall.AirbaseName != null) // AirbaseName not null happens it someone calls ATC on AWACS freq
            {
                responseCallsign = "Overlord"; // Overlord is the default callsign;
            }
            else
            {
                responseCallsign = radioCall.AwacsCallsign;
            }
            return $"{radioCall.Sender.Callsign}, {responseCallsign}, ";
        }
    }
}

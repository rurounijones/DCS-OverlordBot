using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using NLog;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class SetWarningRadius
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> Process(BaseRadioCall baseRadioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {

            SetWarningRadiusRadioCall radioCall = new SetWarningRadiusRadioCall(baseRadioCall);

            Logger.Debug($"Setting up Warning Radius for {radioCall.Sender.Id} - {radioCall.Sender}");

            if (radioCall.WarningRadius == -1)
            {
                return $"I did not catch the warning distance.";
            }
            else
            {
                new WarningRadiusChecker(radioCall.Sender, radioCall.ReceiverName, voice, radioCall.WarningRadius, responseQueue);
                return $"warning set for {radioCall.WarningRadius} miles.";
            }
        }
    }
}

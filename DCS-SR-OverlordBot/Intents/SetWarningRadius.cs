using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog;
using RurouniJones.DCS.OverlordBot.Controllers;
using RurouniJones.DCS.OverlordBot.RadioCalls;

namespace RurouniJones.DCS.OverlordBot.Intents
{
    internal class SetWarningRadius
    {
        private static readonly Random Random = new Random();

        private static readonly List<string> Responses = new List<string>
        {
            "warning set for {0} miles",
            "we'll warn you at {0} miles",
            "perimeter set for {0} miles"
        };

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> Process(BaseRadioCall baseRadioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            return await Task.Run(() =>
            {
                var radioCall = new SetWarningRadiusRadioCall(baseRadioCall);

                Logger.Debug($"Setting up Warning Radius for {radioCall.Sender.Id} - {radioCall.Sender}");

                if (radioCall.WarningRadius == -1)
                {
                    return "I did not catch the warning distance.";
                }

                var _ = new WarningRadiusChecker(radioCall.Sender, radioCall.ReceiverName, voice,
                    radioCall.WarningRadius, responseQueue);

                return string.Format(Responses[Random.Next(Responses.Count)], radioCall.WarningRadius);
            });
        }
    }
}

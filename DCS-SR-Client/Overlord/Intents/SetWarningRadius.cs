using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using NLog;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class SetWarningRadius
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> Process(LuisResponse luisResponse, string callerId, Sender sender, string awacs, string voice, ConcurrentQueue<byte[]> responseQueue)
        {

            Logger.Debug($"Setting up Warning Radius for {callerId} - {sender}");

            int distance = 1;
            if (luisResponse.Entities.Find(x => x.Role == "distance") == null) {
                return $"I did not catch the warning distance";
            }
            else
            {
                distance = int.Parse(luisResponse.Entities.Find(x => x.Role == "distance").Entity);

                new WarningRadiusChecker(callerId, sender, awacs, voice, distance, responseQueue);
                return $"warning set for {distance} miles";
            }

        }
    }
}

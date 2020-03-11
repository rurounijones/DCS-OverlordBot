using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class BearingToFriendlyPlayer
    {
        [Trace]
        public static async Task<string> Process(LuisResponse luisResponse, Sender sender)
        {
            string response;

            var target = luisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];

            Dictionary<string, int?> braData = await GameState.GetFriendlyPlayer(sender.Group, sender.Flight, sender.Plane, "", 0, 0);

            if (braData != null)
            {

                string bearing = Regex.Replace(braData["bearing"].Value.ToString("000"), "\\d{1}", " $0");
                string range = braData["range"].Value.ToString();
                string altitude = braData["altitude"].Value.ToString("N0");

                response = $"Bra, {bearing}, {range}, {altitude}";
            }
            else
            {
                response = $"I cannot find {""} {0} {0}";
            }

            return response;
        }
    }
}
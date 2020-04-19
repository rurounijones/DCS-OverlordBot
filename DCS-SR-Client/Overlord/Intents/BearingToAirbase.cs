using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{

    class BearingToAirbase
    {
        [Trace]
        public static async Task<string> Process(LuisResponse luisResponse, Sender sender)
        {
            string response;

            if (luisResponse.Entities.Find(x => x.Type == "airbase") == null)
            {
                return "I could not recognise the airbase";
            }

            var airbase = luisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];


            Dictionary<string, int> braData = await GameState.GetBearingToAirbase(sender.Position, sender.Group, sender.Flight, sender.Plane, airbase);

            if (braData != null)
            {
                var bearing = Regex.Replace(braData["bearing"].ToString("000"), "\\d{1}", " $0");
                var range = braData["range"];
                response = $"{PronounceAirbase(airbase)} bearing {bearing}, {range} miles";
            }
            else
            {
                response = $"I Could not find {PronounceAirbase(airbase)}";
            }

            return response;
        }

        private static string PronounceAirbase(string airbase)
        {
            // TODO - Try and find the phonetic representation of all airbases on caucasus, including the russian carrier
            switch (airbase.ToLower())
            {
                case "krymsk":
                    airbase = "<phoneme alphabet=\"ipa\" ph=\"ˈkrɨm.sk\">Krymsk</phoneme>";
                    break;
                case "kutaisi":
                    airbase = "<phoneme alphabet=\"ipa\" ph=\"kuˈtaɪ si\">Kutaisi</phoneme>";
                    break;
            }

            return airbase;
        }

    }
}

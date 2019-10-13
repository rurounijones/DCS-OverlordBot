using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{

    class RequestBearingToAirbase
    {
        public static async Task<string> Process(LuisResponse luisResponse, Sender sender)
        {
            string response;

            if (luisResponse.Entities.Find(x => x.Type == "airbase") == null)
            {
                return "I could not recognise the airbase";
            }

            var airbase = luisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];


            Dictionary<string, int> braData = await GameState.GetBearingToAirbase(sender.Group, sender.Flight, sender.Plane, airbase);

            if (braData != null)
            {
                response = $"{PronounceAirbase(airbase)} bearing {Regex.Replace(braData["bearing"].ToString("000"), "\\d{1}", " $0")}";
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

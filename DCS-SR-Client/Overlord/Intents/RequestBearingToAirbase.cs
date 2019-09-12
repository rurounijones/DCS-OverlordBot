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
                response = $"{airbase} bearing {Regex.Replace(braData["bearing"].ToString("000"), "\\d{1}", " $0")}";
            }
            else
            {
                response = $"I Could not find {airbase}";
            }

            return response;
        }

    }
}

using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class RequestBogeyDope
    {

        public static async Task<string> Process(LuisResponse luisResponse, Sender sender)
        {
            string response;

            Dictionary<string, int> braData = await GameState.GetBogeyDope(sender.Group, sender.Flight, sender.Plane);

            if (braData != null)
            {
                response = $"Bra ; {Regex.Replace(braData["bearing"].ToString("000"), "\\d{1}", " $0")} ; {braData["range"].ToString()} ; {braData["altitude"].ToString("N0")}";
            }
            else
            {
                response = "Picture is clean";
            }
            
            return response;
        }
    }
}


using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class RequestBogeyDope
    {
        public class Sender
        {
            public string Group { get; }
            public int Flight { get; }
            public int Plane { get; }

            public Sender(string group, int flight, int plane)
            {
                this.Group = group;
                this.Flight = flight;
                this.Plane = plane;
            }
        }

        public static async Task<string> Process(string json)
        {
            LuisResponse luisResponse = JsonConvert.DeserializeObject<LuisResponse>(json);

            string responseText;

            if (luisResponse.CompositeEntities == null || luisResponse.CompositeEntities.Count == 0 || luisResponse.CompositeEntities[0].Children.Count != 3)
            {
                responseText = "Last transmitter, I could not recognise your call-sign.";
            }
            else
            {
                var awacs = luisResponse.Entities.Find(x => x.Type == "awacs_callsign").Resolution.Values[0];
                string braResponse;

                var sender = await GetSender(luisResponse);
                if ((await GameState.DoesPilotExist(sender.Group, sender.Flight, sender.Plane) == false))
                {
                    braResponse = "I cannot find you on scope";
                }
                else
                {
                    Dictionary<string, int> braData = await GameState.GetBogeyDope(sender.Group, sender.Flight, sender.Plane);
                    if (braData != null)
                    {
                        braResponse = $"Bra - {Regex.Replace(braData["bearing"].ToString("000"), "\\d{1}", " $0")} for {braData["range"].ToString()}, at {braData["altitude"].ToString("N0")}";
                    }
                    else
                    {
                        braResponse = "Picture is clean";
                    }
                }

                responseText = $"{sender.Group} {sender.Flight} {sender.Plane}, {awacs}, {braResponse}";

            }
            return responseText;
        }

        private static async Task<Sender> GetSender(LuisResponse response)
        {
            var group = response.CompositeEntities[0].Children.Find(x => x.ContainsKey("type") && x["type"].Contains("group"))["value"];
            group = await GetNormalisedValue(response, group);

            var flightAndPlane = response.CompositeEntities[0].Children.Find(x => x.ContainsKey("role") && x["role"] == "flight")["value"];
            var flight = (int)char.GetNumericValue(flightAndPlane[0]);
            var plane = (int)char.GetNumericValue(flightAndPlane[1]);

            return new Sender(group, flight, plane);
        }

        private static async Task<string> GetNormalisedValue(LuisResponse response, string entity)
        {
            var definedGroup = response.Entities.Find(x => x.Type == "defined_group");
            if (definedGroup == null)
            {
                return entity;
            }
            else
            {
                return definedGroup.Resolution.Values[0];
            }
        }
    }
}


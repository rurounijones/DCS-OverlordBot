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

        public static async Task<string> Process(LuisResponse luisResponse)
        {
            string responseText;

            if (luisResponse.CompositeEntities == null || luisResponse.CompositeEntities.Count == 0)
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
            var group = response.Entities.Find(x => x.Type == "defined_group").Resolution.Values[0];
            var flight = Int32.Parse(response.Entities.Find(x => x.Role == "flight").Resolution.Value);
            var plane = Int32.Parse(response.Entities.Find(x => x.Role == "element").Resolution.Value);

            return new Sender(group, flight, plane);
        }

    }
}


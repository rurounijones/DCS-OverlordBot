using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    class SenderExtractor
    {

        [Trace]
        public async static Task<Sender> Extract(LuisResponse response)
        {
            string group;
            if (response.Entities.Find(x => x.Type == "defined_group") != null)
            {
                group = response.Entities.Find(x => x.Type == "defined_group").Resolution.Values[0];
            }
            else if (response.Entities.Find(x => x.Type == "learned_group") != null)
            {
                group = response.Entities.Find(x => x.Type == "learned_group").Entity;
            }
            else
            {
                return null;
            }

            Int32 flight;
            Int32 plane;

            if (response.Entities.Find(x => x.Role == "flight") != null && response.Entities.Find(x => x.Role == "element") != null)
            {
                flight = Int32.Parse(response.Entities.Find(x => x.Role == "flight").Resolution.Value);
                plane = Int32.Parse(response.Entities.Find(x => x.Role == "element").Resolution.Value);
            }
            else if (response.Entities.Find(x => x.Role == "flight_and_element") != null &&
                     response.Entities.Find(x => x.Role == "flight_and_element").Entity.Length == 2)
            {
                bool flight_parse = Int32.TryParse(response.Entities.Find(x => x.Role == "flight_and_element").Entity[0].ToString(), out flight);
                bool element_parse = Int32.TryParse(response.Entities.Find(x => x.Role == "flight_and_element").Entity[1].ToString(), out plane);

                if(flight_parse == false || element_parse == false)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            return new Sender(group, flight, plane);
        }
    }
}

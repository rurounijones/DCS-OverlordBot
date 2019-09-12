using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    class SenderExtractor
    {

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
                Int32.Parse(response.Entities.Find(x => x.Role == "flight_and_element").Resolution.Value) >= 10 &&
                Int32.Parse(response.Entities.Find(x => x.Role == "flight_and_element").Resolution.Value) <= 99)
            {
                flight = Int32.Parse(response.Entities.Find(x => x.Role == "flight_and_element").Resolution.Value[0].ToString());
                plane = Int32.Parse(response.Entities.Find(x => x.Role == "flight_and_element").Resolution.Value[1].ToString());
            }
            else
            {
                return null;
            }

            return new Sender(group, flight, plane);
        }
    }
}

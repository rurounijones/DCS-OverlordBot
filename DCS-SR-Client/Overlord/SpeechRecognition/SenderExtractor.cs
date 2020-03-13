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
            string group = null;
            int flight = -1;
            int element = -1;

            if(response.CompositeEntities == null || response.CompositeEntities.Count == 0)
            {
                return null;
            }

            var sender = response.CompositeEntities.Find(x => x.ParentType == "learned_sender" || x.ParentType == "defined_sender");

            if (sender != null)
            {

                sender.Children.ForEach(x =>
                {
                    if (x["type"] == "learned_group" || x["type"] == "defined_group")
                    {
                        group = x["value"];
                    }
                    else if (x["type"] == "awacs_callsign")
                    {
                        // No-op
                    }
                    else if (x["role"] == "flight_and_element")
                    {
                        Int32.TryParse(x["value"][0].ToString(), out flight);
                        Int32.TryParse(x["value"][1].ToString(), out element);
                    }
                    else if (x["role"] == "flight")
                    {
                        int value = SenderExtractor.mapToInt(x["value"]);
                        if (value == -1)
                        {
                            Int32.TryParse(x["value"], out flight);
                        } else
                        {
                            flight = value;
                        }
                    }
                    else if (x["role"] == "element")
                    {
                        int value = SenderExtractor.mapToInt(x["value"]);

                        if (value == -1)
                        {
                            Int32.TryParse(x["value"], out element);
                        }
                        else
                        {
                            element = value;
                        }
                    }
                });
            }

            if(group == null || flight == -1 || element == -1)
            {
                return null;
            } else
            {
                return new Sender(group, flight, element);
            }
        }

        // Because bloody LUIS composite entities don't contain any of the actual goddamned useful information like the actual
        // value of a builtin.number... noooo, you have to go to the entities for that, but which entity maps to which part of
        // the composite? Hahahahaha, good luck finding out.
        // therefore we have to do this stupid bloody hardcoding
        // https://cognitive.uservoice.com/forums/551524-language-understanding-luis/suggestions/39933520-builtin-entities-in-compositeentities-must-have-as
        public static int mapToInt(string value)
        {
            switch(value)
            {
                case "one":
                    return 1;
                case "two":
                    return 2;
                case "three":
                    return 3;
                case "four":
                    return 4;
                case "five":
                    return 5;
                case "six":
                    return 6;
                case "seven":
                    return 7;
                case "eight":
                    return 8;
                case "nine":
                    return 9;
                default:
                    return -1;
            }
        }
    }
}

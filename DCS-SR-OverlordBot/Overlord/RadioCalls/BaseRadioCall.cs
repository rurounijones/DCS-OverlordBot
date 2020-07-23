using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls.LuisModels;
using Newtonsoft.Json;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    public class BaseRadioCall
    {
        /// <summary>
        /// The deserialized response from the Azure Language Understanding application.
        /// </summary>
        public LuisResponse LuisResponse;

        /// <summary>
        /// The intent of the radio transmission
        /// </summary>
        public string Intent
        {
            get
            {
                return LuisResponse.TopScoringIntent["intent"];
            }
        }

        /// <summary>
        /// The player that sent the radio call.
        /// </summary>
        public Player Sender
        {
            get
            {
                if(_sender != null)
                {
                    return _sender;
                }
                if (LuisResponse.CompositeEntities == null || LuisResponse.CompositeEntities.Count == 0)
                {
                    return null;
                }

                var sender = LuisResponse.CompositeEntities.Find(x => x.ParentType == "learned_sender" || x.ParentType == "defined_sender" || x.ParentType == "airbase_caller");

                _sender = BuildPlayer(sender);
                return _sender;
            }
            set
            {
                _sender = value;
            }
        }
        private Player _sender;


        /// <summary>
        /// The name of the bot that the player is attempting to contact.
        /// </summary>
        /// <example>
        /// For an AWACS bot this is the Callsign such as "Overlord" or "Magic".
        /// For an ATC bot this is the normalized name of an airfield such as "Krasnodar-Center"
        /// </example>
        public string ReceiverName {
            get
            {
                if (LuisResponse.Entities.Find(x => x.Type == "awacs_callsign") != null)
                {
                    return LuisResponse.Entities.Find(x => x.Type == "awacs_callsign").Resolution.Values[0];
                }
                else if (LuisResponse.Entities.Find(x => x.Type == "airbase") != null)
                {
                    return LuisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];
                }
                else
                {
                    return null;
                }
            }
        }

        public BaseRadioCall(string luisJson)
        {
            LuisResponse = JsonConvert.DeserializeObject<LuisResponse>(luisJson);

        }

        public BaseRadioCall(BaseRadioCall baseRadioCall)
        {
            LuisResponse = baseRadioCall.LuisResponse;
            Sender = baseRadioCall.Sender;
        }

        protected static Player BuildPlayer(LuisCompositeEntity luisEntity)
        {
            string group = null;
            int flight = -1;
            int element = -1;

            luisEntity.Children.ForEach(x =>
            {
                if (x["type"] == "learned_group" || x["type"] == "defined_group")
                {
                    group = x["value"];
                }
                else if (x["type"] == "awacs_callsign" || x["type"] == "airbase" || x["type"] == "airbase_control_name")
                {
                    // No-op
                }
                else if (x["role"] == "flight_and_element")
                {
                    int.TryParse(x["value"][0].ToString(), out flight);
                    int.TryParse(x["value"][1].ToString(), out element);
                }
                else if (x["role"] == "flight")
                {
                    int value = MapToInt(x["value"]);
                    if (value == -1)
                    {
                        int.TryParse(x["value"], out flight);
                    }
                    else
                    {
                        flight = value;
                    }
                }
                else if (x["role"] == "element")
                {
                    int value = MapToInt(x["value"]);

                    if (value == -1)
                    {
                        int.TryParse(x["value"], out element);
                    }
                    else
                    {
                        element = value;
                    }
                }
            });

            if (group == null || flight == -1 || element == -1)
            {
                return null;
            }

            return new Player()
            {
                Group = group,
                Flight = flight,
                Plane = element
            };
        }


        // Because bloody LUIS composite entities don't contain any of the actual goddamned useful information like the actual
        // value of a builtin.number... noooo, you have to go to the entities for that, but which entity maps to which part of
        // the composite? Hahahahaha, good luck finding out. Therefore we have to do this stupid bloody hardcoding
        //
        // I believe this has been fixed in the latest LUIS API version so we should be able to get rid of this when we update
        // https://cognitive.uservoice.com/forums/551524-language-understanding-luis/suggestions/39933520-builtin-entities-in-compositeentities-must-have-as
        private static int MapToInt(string value)
        {
            switch (value)
            {
                case "zero":
                    return 0;
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

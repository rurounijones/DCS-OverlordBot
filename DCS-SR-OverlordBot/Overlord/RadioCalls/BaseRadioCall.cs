using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls.LuisModels;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    public class BaseRadioCall : IRadioCall
    {
        /// <summary>
        /// The deserialized response from the Azure Language Understanding application.
        /// </summary>
        public LuisResponse LuisResponse { get; }

        /// <summary>
        /// The intent of the radio transmission
        /// </summary>
        public virtual string Intent => LuisResponse.TopScoringIntent["intent"];

        public string Message => LuisResponse.Query;

        /// <summary>
        /// The player that sent the radio call.
        /// </summary>
        public Player Sender
        {
            get
            {
                if (_sender != null)
                    return _sender;
                if (LuisResponse.CompositeEntities == null || LuisResponse.CompositeEntities.Count == 0)
                    return null;

                _sender = BuildPlayer(LuisResponse.CompositeEntities.Find(x => x.ParentType == "learned_sender" || 
                                                                               x.ParentType == "defined_sender" || 
                                                                               x.ParentType == "airbase_caller"));


                return _sender;
            }
            set => _sender = value;
        }
        private Player _sender;


        /// <summary>
        /// The name of the bot that the player is attempting to contact.
        /// </summary>
        /// <example>
        /// For an AWACS bot this is the Callsign such as "Overlord" or "Magic".
        /// For an ATC bot this is the normalized name of an airfield such as "Krasnodar-Center"
        /// </example>
        public virtual string ReceiverName => AwacsCallsign ?? AirbaseName;

        public virtual string AwacsCallsign => LuisResponse.Entities.Find(x => x.Type == "awacs_callsign")?.Resolution.Values[0];

        public virtual string AirbaseName => LuisResponse.Entities.Find(x => x.Type == "airbase")?.Resolution.Values[0];

        public BaseRadioCall(string luisJson)
        {
            LuisResponse = JsonConvert.DeserializeObject<LuisResponse>(luisJson);
            Task.Run(async () => await GameQuerier.PopulatePilotData(this)).Wait();
        }

        public BaseRadioCall(IRadioCall baseRadioCall)
        {
            LuisResponse = baseRadioCall.LuisResponse;
            Sender = baseRadioCall.Sender;
        }

        protected static Player BuildPlayer(LuisCompositeEntity luisEntity)
        {
            string group = null;
            var flight = -1;
            var element = -1;

            luisEntity.Children.ForEach(x =>
            {
                switch (x["type"])
                {
                    case "learned_group":
                    case "defined_group":
                        @group = x["value"];
                        break;
                    case "awacs_callsign":
                    case "airbase":
                    case "airbase_control_name":
                        // No-op
                        break;
                    default:
                    {
                        switch (x["role"])
                        {
                            case "flight_and_element":
                                int.TryParse(x["value"][0].ToString(), out flight);
                                int.TryParse(x["value"][1].ToString(), out element);
                                break;
                            case "flight":
                            {
                                var value = MapToInt(x["value"]);
                                if (value == -1)
                                {
                                    int.TryParse(x["value"], out flight);
                                }
                                else
                                {
                                    flight = value;
                                }

                                break;
                            }
                            case "element":
                            {
                                var value = MapToInt(x["value"]);

                                if (value == -1)
                                {
                                    int.TryParse(x["value"], out element);
                                }
                                else
                                {
                                    element = value;
                                }

                                break;
                            }
                        }

                        break;
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

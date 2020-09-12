using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.RadioCalls;
using RurouniJones.DCS.OverlordBot.SpeechOutput;
using RurouniJones.DCS.OverlordBot.Util;

namespace RurouniJones.DCS.OverlordBot.Intents
{
    internal class BearingToAirbase
    {
        public static async Task<string> Process(IRadioCall radioCall)
        {
            switch (radioCall.AirbaseName)
            {
                case null:
                    return "I could not recognize the airbase name";
                case "nearest":
                    return await NearestAirbase(radioCall);
                default:
                    return await NamedAirbase(radioCall);
            }
        }

        private static async Task<string> NearestAirbase(IRadioCall radioCall)
        {
            string response;
            var braData = await GameQuerier.GetBearingToNearestFriendlyAirbase(radioCall.Sender.Position,
                radioCall.Sender.Group, radioCall.Sender.Flight, radioCall.Sender.Plane, (int) radioCall.Sender.Coalition);

            if (braData != null)
            {
                var bearing =
                    Regex.Replace(Geospatial.TrueToMagnetic(radioCall.Sender.Position, (int) braData["bearing"]).ToString("000"),
                        "\\d{1}", " $0");
                var range = braData["range"];
                response = $"{AirbasePronouncer.PronounceAirbase((string) braData["name"])} bearing {bearing}, {(int) range} miles";
            }
            else
            {
                response = "I Could not find any friendly airbases.";
            }

            return response;
        }

        private static async Task<string> NamedAirbase(IRadioCall radioCall)
        {
            string response;
            var braData = await GameQuerier.GetBearingToNamedAirbase(radioCall.Sender.Position,
                radioCall.Sender.Group, radioCall.Sender.Flight, radioCall.Sender.Plane, radioCall.AirbaseName);

            if (braData != null)
            {
                var bearing =
                    Regex.Replace(Geospatial.TrueToMagnetic(radioCall.Sender.Position, braData["bearing"]).ToString("000"),
                        "\\d{1}", " $0");
                var range = braData["range"];
                response = $"{AirbasePronouncer.PronounceAirbase(radioCall.AirbaseName)} bearing {bearing}, {range} miles";
            }
            else
            {
                response = $"I Could not find {AirbasePronouncer.PronounceAirbase(radioCall.AirbaseName)}.";
            }

            return response;
        }
    }
}

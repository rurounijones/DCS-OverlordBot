using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{

    class BearingToAirbase
    {
        public static async Task<string> Process(IRadioCall radioCall)
        {
            string response;

            if (radioCall.AirbaseName == null)
            {
                return "I could not recognise the airbase";
            }

            Dictionary<string, int> braData = await GameQuerier.GetBearingToAirbase(radioCall.Sender.Position, radioCall.Sender.Group, radioCall.Sender.Flight, radioCall.Sender.Plane, radioCall.AirbaseName);

            if (braData != null)
            {
                var bearing = Regex.Replace(Util.Geospatial.TrueToMagnetic(radioCall.Sender.Position, braData["bearing"]).ToString("000"), "\\d{1}", " $0");
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

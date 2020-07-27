using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{

    class BearingToAirbase
    {
        public static async Task<string> Process(BaseRadioCall radioCall)
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
                response = $"{PronounceAirbase(radioCall.AirbaseName)} bearing {bearing}, {range} miles";
            }
            else
            {
                response = $"I Could not find {PronounceAirbase(radioCall.AirbaseName)}.";
            }

            return response;
        }

        public static string PronounceAirbase(string airbase)
        {
            // TODO - Try and find the phonetic representation of all airbases on caucasus, including the russian carrier
            switch (airbase.ToLower())
            {
                case "krymsk":
                    airbase = "<phoneme alphabet=\"ipa\" ph=\"ˈkrɨm.sk\">Krymsk</phoneme>";
                    break;
                case "kutaisi":
                    airbase = "<phoneme alphabet=\"ipa\" ph=\"kuˈtaɪ si\">Kutaisi</phoneme>";
                    break;
                case "mineralnye vody":
                    airbase = "<phoneme alphabet=\"ipa\" ph=\"mʲɪnʲɪˈralʲnɨjə ˈvodɨ\">Mineralnye Vody</phoneme>";
                    break;
                case "gelendzhik":
                    airbase = "<phoneme alphabet=\"ipa\" ph=\"ɡʲɪlʲɪnd͡ʐˈʐɨk\">Gelendzhik</phoneme>";
                    break;
            }

            return airbase;
        }

    }
}

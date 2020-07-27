using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class BearingToFriendlyPlayer
    {
        public static async Task<string> Process(BearingToFriendlyPlayerRadioCall radioCall)
        {
            string response;

            if (radioCall.FriendlyPlayer == null)
            {
                return "I could not understand the friendly's callsign";
            }
            else
            {
                Contact contact = await GameQuerier.GetFriendlyPlayer(radioCall.Sender.Group, radioCall.Sender.Flight, radioCall.Sender.Plane,
                    radioCall.FriendlyPlayer.Group, radioCall.FriendlyPlayer.Flight, radioCall.FriendlyPlayer.Plane);

                if (contact != null)
                {

                    string bearing = Regex.Replace(Util.Geospatial.TrueToMagnetic(radioCall.Sender.Position, contact.Bearing).ToString("000"), "\\d{1}", " $0");
                    string range = contact.Range.ToString();
                    int altitude = (int)contact.Altitude;
                    int angels;
                    if (altitude < 1000)
                    {
                        angels = 1;
                    }
                    else
                    {
                        angels = (altitude % 1000 >= 500 ? altitude + 1000 - altitude % 1000 : altitude - altitude % 1000) / 1000;
                    }

                    response = $"Bra, {bearing}, {range}, angels {angels}.";
                }
                else
                {
                    response = $"I cannot find {""} {0} {0}.";
                }
            }

            return response;
        }
    }
}
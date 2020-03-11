using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class BogeyDope
    {
        [Trace]
        public static async Task<string> Process(LuisResponse luisResponse, Sender sender)
        {
            string response;

            Dictionary<string, int?> braData = await GameState.GetBogeyDope(sender.Group, sender.Flight, sender.Plane);

            if (braData != null)
            {

                string bearing = Regex.Replace(braData["bearing"].Value.ToString("000"), "\\d{1}", " $0");
                string range = braData["range"].Value.ToString();
                string altitude = braData["altitude"].Value.ToString("N0");
                string aspect = GetAspect(braData);

                response = $"Bra, {bearing}, {range}, {altitude}{aspect}";
            }
            else
            {
                response = "Picture is clean";
            }
            
            return response;
        }

        private static string GetAspect(Dictionary<string, int?>  braData)
        {
            if (braData["heading"].HasValue == false)
            {
                return null;
            }

            int bearing = braData["bearing"].Value;
            int heading = braData["heading"].Value;

            // Allows us to just use clockwise based positive calculations
            if (heading < bearing)
            {
                heading += 360;
            }

            string aspect;

            if (heading <= bearing + 45)
            {
                aspect = "cold";
            }
            else if (heading >= bearing + 45 && heading <= bearing + 135)
            {
                aspect = "flanking right";
            }
            else if (heading >= bearing + 135 && heading <= bearing + 225)
            {
                aspect = "hot";
            }
            else if (heading >= bearing + 225)
            {
                aspect = "flanking left";
            }
            else
            {
                aspect = null;
            }

            return ", " + aspect;

        }

    }
}


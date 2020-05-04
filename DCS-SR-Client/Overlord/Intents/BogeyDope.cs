using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class BogeyDope
    {
        [Trace]
        public static async Task<string> Process(Player sender)
        {
            string response;

            Contact contact = await GameQuerier.GetBogeyDope(sender.Position, sender.Group, sender.Flight, sender.Plane);

            if (contact != null)
            {
                response = BuildResponse(sender, contact);
            }
            else
            {
                response = "Picture is clean";
            }
            
            return response;
        }

        public static string BuildResponse(Player sender, Contact contact)
        {
            string bearing = Regex.Replace(Util.Geospatial.TrueToMagnetic(sender.Position, contact.Bearing).ToString("000"), "\\d{1}", " $0");
            string range = contact.Range.ToString();
            string altitude = contact.Altitude.ToString("N0");
            string aspect = GetAspect(contact);
            string name = PronounceName(contact);

            var response = $"Bra, {bearing}, {range}, {altitude}{aspect}";

            if(name != null)
            {
                response += $", type <break time=\"50\" /> {name}";
            }

            return response;
        }

        private static string GetAspect(Contact contact)
        {
            if (contact.Heading.HasValue == false)
            {
                return null;
            }

            int bearing = contact.Bearing;
            int heading = contact.Heading.Value;

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

        private static string PronounceName(Contact contact)
        {
            if(contact.Name == null)
            {
                return null;
            }

            string s;
            switch (contact.Name)
            {
                case "MiG-29S":
                    s = "Fulcrum";
                    break;
                case "Su-27":
                    s = "Flanker";
                    break;
                case "Su-33":
                    s = "Flanker";
                    break;
                case "J-11A":
                    s = "Flanker";
                    break;
                case "MiG-21Bis":
                    s = "Fishbed";
                    break;
                case "A-50":
                    s = "Mainstay";
                    break;
                case "Su-25":
                    s = "Frogfoot";
                    break;
                case "Su-25T":
                    s = "Frogfoot";
                    break;
                case "MiG-29A":
                    s = "Fulcrum";
                    break;
                case "MiG-31":
                    s = "Foxhound";
                    break;
                case "An-30M":
                    s = "Clank";
                    break;
                case "F-5E-3":
                    s = "Tiger 2";
                    break;
                case "Mi-8MTV2":
                    s = "Hip";
                    break;
                case "Mi-24V":
                    s = "Hind";
                    break;
                case "Su-24M":
                    s = "Fencer";
                    break;
                case "Ka-50":
                    s = "Hokum A";
                    break;
                case "Ka-52":
                    s = "Hokum B";
                    break;
                default:
                    s = contact.Name;
                    break;
            }
            return s;
        }

    }
}


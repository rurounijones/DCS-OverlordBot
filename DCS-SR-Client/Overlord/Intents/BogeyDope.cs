using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Awacs;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using NewRelic.Api.Agent;
using Newtonsoft.Json;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class BogeyDope
    {
        public static readonly List<Aircraft> AircraftMapping = JsonConvert.DeserializeObject<List<Aircraft>>(File.ReadAllText("Overlord/Data/Aircraft.json"));

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
            if (contact.Name == null || contact.Name.Length == 0)
                return "unknown";
            var aircraft = AircraftMapping.FirstOrDefault(ac => ac.DcsId.Equals(contact.Name));
            if (aircraft != null && aircraft.NatoName.Length > 0)
                return aircraft.NatoName;
            else
                return contact.Name;
        }

    }
}


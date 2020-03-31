using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewRelic.Api.Agent;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class BogeyDope
    {
        [Trace]
        public static async Task<string> Process(Sender sender)
        {
            string response;

            Contact contact = await GameState.GetBogeyDope(sender.Group, sender.Flight, sender.Plane);

            if (contact != null)
            {
                response = BuildResponse(contact);
            }
            else
            {
                response = "Picture is clean";
            }
            
            return response;
        }

        public static string BuildResponse(Contact contact)
        {
            string bearing = Regex.Replace(contact.Bearing.ToString("000"), "\\d{1}", " $0");
            string range = contact.Range.ToString();
            string altitude = contact.Altitude.ToString("N0");
            string aspect = GetAspect(contact);

            return $"Bra, {bearing}, {range}, {altitude}{aspect}";
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

    }
}


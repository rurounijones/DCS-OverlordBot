using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class BogeyDope
    {

        public static async Task<string> Process(BaseRadioCall radioCall)
        {
            string response;
            Player sender = radioCall.Sender;

            Contact contact = await GameQuerier.GetBogeyDope(sender.Coalition, sender.Group, sender.Flight, sender.Plane);

            if (contact != null)
            {
                response = BuildResponse(sender, contact);
            }
            else
            {
                response = "Picture is clean.";
            }

            return response;
        }

        public static string BuildResponse(Player sender, Contact contact)
        {
            string bearing = Regex.Replace(Util.Geospatial.TrueToMagnetic(sender.Position, contact.Bearing).ToString("000"), "\\d{1}", " $0");
            string range = contact.Range.ToString();
            string altitude = contact.Altitude.ToString("N0");
            string aspect = GetAspect(contact);
            string name = AircraftReportingNamePronouncer.PronounceName(contact);

            var response = $"Bra, {bearing}, {range}, {altitude}{aspect}";

            if (name != null)
            {
                response += $", type <break time=\"50\" /> {name}.";
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
    }
}


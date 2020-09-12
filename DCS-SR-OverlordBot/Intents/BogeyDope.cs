using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.RadioCalls;
using RurouniJones.DCS.OverlordBot.SpeechOutput;
using RurouniJones.DCS.OverlordBot.Util;

namespace RurouniJones.DCS.OverlordBot.Intents
{
    internal class BogeyDope
    {

        public static async Task<string> Process(IRadioCall radioCall)
        {
            var sender = radioCall.Sender;

            var contact = await GameQuerier.GetBogeyDope(sender.Coalition, sender.Group, sender.Flight, sender.Plane);

            return contact != null ? BuildResponse(sender, contact) : "Picture is clean.";
        }

        public static string BuildResponse(Player sender, Contact contact)
        {
            var bearing = Regex.Replace(Geospatial.TrueToMagnetic(sender.Position, contact.Bearing).ToString("000"), "\\d{1}", " $0");
            var range = contact.Range.ToString();
            var altitude = contact.Altitude.ToString("N0");
            var aspect = GetAspect(contact);
            var name = AircraftReportingNamePronouncer.PronounceName(contact);

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

            var bearing = contact.Bearing;
            var heading = contact.Heading.Value;

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


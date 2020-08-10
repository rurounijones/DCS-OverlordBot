using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Util;
using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    internal class Declare
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> Process(DeclareRadioCall radioCall)
        {
            var bearing = radioCall.BearingToTarget;
            var distance = radioCall.DistanceToTarget;

            var declarePoint = Geospatial.CalculatePointFromSource(radioCall.Sender.Position, NauticalMilesToMeters(distance), Geospatial.MagneticToTrue(radioCall.Sender.Position, bearing));

            var radius = 1 + (distance * 0.05);

            Logger.Info($"Declare Source (Lon/Lat): {radioCall.Sender.Position}, Magnetic Bearing {bearing}, True Bearing {Geospatial.MagneticToTrue(radioCall.Sender.Position, bearing)},\n Declare Target (Lon/Lat): {declarePoint}, Search radius: {radius} miles");

            var contacts = await GameQuerier.GetContactsWithinCircle(declarePoint, NauticalMilesToMeters(radius));

            contacts = contacts.Where(contact => contact.Id != radioCall.Sender.Id).ToList();

            if (contacts.Count == 0)
            {
                return "no contacts found";
            }

            var coalitionContacts = new Dictionary<Coalition, int>
            {
                { Coalition.Neutral, contacts.Count(contact => contact.Coalition == Coalition.Neutral) },
                { Coalition.Redfor, contacts.Count(contact => contact.Coalition == Coalition.Redfor) },
                { Coalition.Bluefor, contacts.Count(contact => contact.Coalition == Coalition.Bluefor) },
            };

            var neutrals = false;
            var friendlies = false;
            var enemies = false;

            if (coalitionContacts[radioCall.Sender.Coalition] > 0)
            {
                friendlies = true;
            }

            if (coalitionContacts.Where(pair => pair.Key == radioCall.Sender.Coalition.GetOpposingCoalition()).Count(pair => pair.Value > 0) > 0)
            {
                enemies = true;
            }

            if (coalitionContacts.Where(pair => pair.Key != radioCall.Sender.Coalition && pair.Key != radioCall.Sender.Coalition.GetOpposingCoalition()).Count(pair => pair.Value > 0) > 0)
            {
                neutrals = true;
            }

            if (enemies && (friendlies || neutrals))
            {
                return "furball.";
            }
            if (!enemies && (friendlies || neutrals))
            {
                return "friendly.";
            }
            return enemies ? "hostile." : "unknown.";
        }
        private static double NauticalMilesToMeters(double nauticalMiles)
        {
            return nauticalMiles * 1.852001 * 1000;
        }
    }
}

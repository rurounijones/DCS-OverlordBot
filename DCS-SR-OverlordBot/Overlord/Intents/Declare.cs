using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Util;
using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class Declare
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> Process(DeclareRadioCall radioCall)
        {
            int bearing = radioCall.BearingToTarget;
            double distance = radioCall.DistanceToTarget;

            Geo.Geometries.Point declarePoint = Geospatial.CalculatePointFromSource(radioCall.Sender.Position, NauticalMilesToMeters(distance), Geospatial.MagneticToTrue(radioCall.Sender.Position, bearing));

            double radius = 1 + (distance * 0.05);

            Logger.Info($"Declare Source (Lon/Lat): {radioCall.Sender.Position}, Magnetic Bearing {bearing}, True Bearing {Geospatial.MagneticToTrue(radioCall.Sender.Position, bearing)},\n Declare Target (Lon/Lat): {declarePoint}, Search radius: {radius} miles");

            var contacts = await GameQuerier.GetContactsWithinCircle(declarePoint, NauticalMilesToMeters(radius));

            contacts = contacts.Where(contact => contact.Id != radioCall.Sender.Id).ToList();

            if (contacts.Count == 0)
            {
                return "no contacts found";
            }

            Dictionary<Coalition, int> coalitionContacts = new Dictionary<Coalition, int>
            {
                { Coalition.Neutral, contacts.Where(contact => contact.Coalition == Coalition.Neutral).Count() },
                { Coalition.Redfor, contacts.Where(contact => contact.Coalition == Coalition.Redfor).Count() },
                { Coalition.Bluefor, contacts.Where(contact => contact.Coalition == Coalition.Bluefor).Count() },
            };

            bool neutrals = false;
            bool friendlies = false;
            bool enemies = false;

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

            if (enemies == true && (friendlies == true || neutrals == true))
            {
                return "furball";
            }
            else if (enemies == false && (friendlies == true || neutrals == true))
            {
                return "friendly";
            }
            else if (enemies == true && (friendlies == false && neutrals == false))
            {
                return "hostile";
            }
            else
            {
                return "unknown";
            }
        }
        private static double NauticalMilesToMeters(double nauticalMiles)
        {
            return nauticalMiles * 1.852001 * 1000;
        }
    }
}

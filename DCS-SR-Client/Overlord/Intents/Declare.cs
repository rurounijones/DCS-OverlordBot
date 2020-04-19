using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Util;
using NetTopologySuite.Geometries;
using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class Declare
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> Process(LuisResponse luisResponse, GameObject caller)
        {

            string bearingString = null;
            if (luisResponse.Entities.Find(x => x.Role == "bearing") == null) {
                return "please provide a bearing";
            }
            else
            {
                bearingString = luisResponse.Entities.Find(x => x.Role == "bearing").Entity;
            }
            int.TryParse(bearingString, out int bearing);

            string distanceString = null;
            if (luisResponse.Entities.Find(x => x.Role == "distance") != null)
            {
                distanceString = luisResponse.Entities.Find(x => x.Role == "distance").Entity;
            }

            // If no distance is provided then we will assume a distance of one mile, which with the radius
            // also being one mile means a check from right in front of the caller out to 2 miles which is
            // probably enough for A-10s, Harriers and other visual only planes
            double distance;
            if (distanceString == null) {
                distance = 1; // Nautical Mile
            }
            else
            {
                double.TryParse(distanceString, out distance);
            }

            Point declarePoint = Geospatial.CalculatePointFromSource(caller.Position, NauticalMilesToMeters(distance), Geospatial.MagneticToTrue(bearing));

            double radius = 1 + (distance * 0.05);

            Logger.Info($"Declare Source (Lon/Lat): {caller.Position}, Magnetic Bearing {bearing}, True Bearing {Geospatial.MagneticToTrue(bearing)},\n Declare Target (Lon/Lat): {declarePoint}, Search radius: {radius} miles");

            var contacts = await GetContactsWithinCircle(declarePoint, NauticalMilesToMeters(radius));

            contacts = contacts.Where(contact => contact.Id != caller.Id).ToList();

            if (contacts.Count == 0)
            {
                return "no contacts found";
            }

            Dictionary<int, int> coalitionContacts = new Dictionary<int, int>
            {
                { 0, contacts.Where(contact => contact.Coalition == 0).Count() },
                { 1, contacts.Where(contact => contact.Coalition == 1).Count() },
                { 2, contacts.Where(contact => contact.Coalition == 2).Count() },
            };

            bool friendlies = false;
            bool enemies = false;

            if(coalitionContacts[caller.Coalition] > 0)
            {
                friendlies = true;
            }

            if (coalitionContacts.Where(pair => pair.Key != caller.Coalition && pair.Key != 2).Count(pair => pair.Value > 0) > 0)
            {
                enemies = true;
            }

            if(enemies == true && friendlies == true)
            {
                return "furball";
            }
            else if (enemies == false && friendlies == true)
            {
                return "friendly";
            }
            else if (enemies == true && friendlies == false)
            {
                return "hostile";
            } else
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

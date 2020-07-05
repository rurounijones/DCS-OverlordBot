using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation;
using Geo;
using Geo.Geometries;
using NLog;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class ReadytoTaxi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static async Task<string> Process(LuisResponse luisResponse, Player sender)
        {

            var airbaseName = luisResponse.Entities.First(x => x.Type == "airbase").Resolution.Values[0];
            var airfield = Manager.Airfields.First(x => x.Name == airbaseName);

            // Find where this player is
            TaxiPoint source = airfield.ParkingSpots.Find(parkingSpot => IsPlayerInBounds(parkingSpot.Area, sender.Position));

            if(source == null)
            {
                return $"I could not find you in any of the parking areas";
            }

            TaxiPoint target = airfield.Runways[0];

            return airfield.GetTaxiInstructions(source, target);
        }

        private static bool IsPlayerInBounds(Polygon area, Point player)
        {
            if(area == null)
            {
                return false;
            }
            return area.GetBounds().Contains(player);
        }

    }
}

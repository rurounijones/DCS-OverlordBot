using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class InboundToAirbase
    {
        public static async Task<string> Process(LuisResponse luisResponse, GameState.Player sender)
        {
            var airbaseName = luisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];
            var airbaseControlName = luisResponse.Entities.Find(x => x.Type == "airbase_control_name").Entity;

            var airfield = Manager.Airfields.Find(x => x.Name == airbaseName);
            var state = new AircraftState(airfield, sender, AircraftState.State.Inbound);
            airfield.Aircraft[sender.Id] = state;

            var bearing = Util.Geospatial.BearingTo(sender.Position.Coordinate, airfield.LandingPatternPoints.Find(x => x.Name == "DownwindEntry").Position.Center);
            bearing = Util.Geospatial.TrueToMagnetic(sender.Position, bearing);
            var heading = Regex.Replace(bearing.ToString("000"), "\\d{1}", " $0");

            return $"{airbaseName} {airbaseControlName}, fly heading {heading}, descend to 1000, speed 2 5 0 knots";
        }
    }
}

using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class InboundToAirbase
    {
        public static async Task<string> Process(LuisResponse luisResponse, Sender sender)
        {
            var airbaseName = luisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];
            var airbaseControlName = luisResponse.Entities.Find(x => x.Type == "airbase_control_name").Entity;

            var airfield = Manager.Airfields.Find(x => x.Name == airbaseName);
            var state = new AircraftState(airfield, sender.GameObject, AircraftState.State.Inbound);
            airfield.Aircraft[sender.GameObject.Id] = state;

            var heading = Regex.Replace(90.ToString("000"), "\\d{1}", " $0");

            return $"{airbaseName} {airbaseControlName}, fly heading {heading}, descend to 1000, maintain 2 5 0 knots";
        }
    }
}

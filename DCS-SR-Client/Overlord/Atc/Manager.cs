using Newtonsoft.Json;
using NLog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc
{

    class Manager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly List<Airfield> Airfields = JsonConvert.DeserializeObject<List<Airfield>>(File.ReadAllText("Overlord/Data/Airfields.json"));
        private static readonly List<NavigationPoint> NavigationPoints = JsonConvert.DeserializeObject<List<NavigationPoint>>(File.ReadAllText("Overlord/Data/AtcAnapa.json"));

        public Manager()
        {
            Task.Run(() => CheckNavigationPointsAsync());
        }

        private async Task CheckNavigationPointsAsync()
        {
            while(true)
            {
                Logger.Debug($"Checking Airfields");

                var anapa = Airfields.FirstOrDefault(x => x.Name == "Anapa-Vityazevo");

                List<GameObject> neabyAircraft = await GameState.GetAircraftNearAirfield(anapa);

                foreach (var aircarft in neabyAircraft)
                {
                    Logger.Debug($"Aircraft {aircarft.Id} (Pilot {aircarft.Pilot}) is within 10nm of Anapa");
                }

                Thread.Sleep(5000);
            }
        }
    }


}

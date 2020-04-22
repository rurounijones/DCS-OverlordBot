using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation;
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
        private static readonly List<Airfield> Airfields = PopulateAirfields();

        public Manager()
        {
            Task.Run(() => CheckNavigationPointsAsync());
        }

        static private List<Airfield> PopulateAirfields()
        {
            List<Airfield> airfields = new List<Airfield>
            {
                JsonConvert.DeserializeObject<Airfield>(File.ReadAllText("Overlord/Data/NavigationPoints/Anapa-Vityazevo.json")),
                JsonConvert.DeserializeObject<Airfield>(File.ReadAllText("Overlord/Data/NavigationPoints/Krasnodar-Center.json"))
            };
            return airfields;
        }

        private async Task CheckNavigationPointsAsync()
        {
            while (true)
            {
                Thread.Sleep(5000);
                Logger.Trace($"Checking Airfields");

                foreach (var airfield in Airfields)
                {
                    List<GameObject> neabyAircraft = await GameState.GetAircraftNearAirfield(airfield);

                    if(neabyAircraft.Count == 0)
                    {
                        Logger.Trace($"No Aircraft within 10nm of {airfield.Name}");
                    }

                    foreach (var aircarft in neabyAircraft)
                    {
                        Logger.Trace($"Aircraft {aircarft.Id} (Pilot {aircarft.Pilot}) is within 10nm of {airfield.Name}");
                        foreach (var navigationPoint in airfield.TaxiwayPoints)
                        {
                            if(navigationPoint.Position.GetBounds().Contains(aircarft.GeoPoint))
                            {
                                var msg = $"Aircraft {aircarft.Id} (Pilot {aircarft.Pilot}) is at {airfield.Name}, {navigationPoint.Name}";
                                Logger.Debug(msg);
                                _ = Discord.DiscordClient.SendNavigationPoint(msg);
                            }
                        }
                    }
                }

            }
        }
    }


}

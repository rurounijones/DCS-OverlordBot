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

        private static readonly List<Airfield> Airfields = JsonConvert.DeserializeObject<List<Airfield>>(File.ReadAllText("Overlord/Data/Airfields.json"));
        private static readonly Dictionary<string, List<NavigationPoint>> AirfieldNavigationPoints = PopulateAirfieldNavigationPoints();

        public Manager()
        {
            Task.Run(() => CheckNavigationPointsAsync());
        }

        static private Dictionary<string, List<NavigationPoint>> PopulateAirfieldNavigationPoints()
        {
            Dictionary<string, List<NavigationPoint>> navigationPoints = new Dictionary<string, List<NavigationPoint>>
            {
                { "Anapa-Vityazevo", JsonConvert.DeserializeObject<List<NavigationPoint>>(File.ReadAllText("Overlord/Data/NavigationPoints/Anapa-Vityazevo.json")) },
                { "Krasnodar-Center", JsonConvert.DeserializeObject<List<NavigationPoint>>(File.ReadAllText("Overlord/Data/NavigationPoints/Krasnodar-Center.json")) }
            };
            return navigationPoints;
        }

        private async Task CheckNavigationPointsAsync()
        {
            while (true)
            {
                Thread.Sleep(5000);
                Logger.Debug($"Checking Airfields");

                var airfields = Airfields.Where(x => AirfieldNavigationPoints.ContainsKey(x.Name));

                foreach (var airfield in airfields)
                {
                    List<GameObject> neabyAircraft = await GameState.GetAircraftNearAirfield(airfield);

                    if(neabyAircraft.Count == 0)
                    {
                        Logger.Debug($"No Aircraft within 10nm of {airfield.Name}");
                    }

                    foreach (var aircarft in neabyAircraft)
                    {
                        Logger.Debug($"Aircraft {aircarft.Id} (Pilot {aircarft.Pilot}) is within 10nm of {airfield.Name}");
                        foreach (var navigationPoint in AirfieldNavigationPoints[airfield.Name])
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

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
                //JsonConvert.DeserializeObject<Airfield>(File.ReadAllText("Overlord/Data/Airfields/Anapa-Vityazevo.json")),
                JsonConvert.DeserializeObject<Airfield>(File.ReadAllText("Overlord/Data/Airfields/Krasnodar-Center.json"))
            };
            return airfields;
        }

        private async Task CheckNavigationPointsAsync()
        {
            while (true)
            {
                Thread.Sleep(2000);
                Logger.Trace($"Checking Airfields");

                foreach (var airfield in Airfields)
                {
                    Logger.Trace($"Checking {airfield.Name}");

                    List<GameObject> neabyAircraft = await GameState.GetAircraftNearAirfield(airfield);

                    if(neabyAircraft.Count == 0)
                    {
                        Logger.Trace($"No Aircraft within 10nm of {airfield.Name}");
                    }

                    foreach (var aircarft in neabyAircraft)
                    {
                        if (!airfield.Aircraft.ContainsKey(aircarft.Id))
                        {
                            AircraftState.State state;

                            if (aircarft.Altitude > airfield.Altitude + 30)
                            {
                                state = AircraftState.State.Flying;
                            }
                            else
                            {
                                state = AircraftState.State.OnGround;
                            }
                            _ = SendToDiscord($"New Aircraft, ID: {aircarft.Id} (Pilot: {aircarft.Pilot}), State: {state}, Airfield: {airfield.Name}");
                            airfield.Aircraft.Add(aircarft.Id, new AircraftState(state));
                        }

                        var stateMessage = $"Aircraft ID: {aircarft.Id} (Pilot {aircarft.Pilot}) is at state: {airfield.Aircraft[aircarft.Id].CurrentState}";
                        Logger.Debug(stateMessage);

                        if (aircarft.Altitude > airfield.Altitude + 30) { 
                            if (airfield.Aircraft[aircarft.Id].CurrentState == AircraftState.State.OnRunway)
                            {
                                airfield.Aircraft[aircarft.Id].Update(AircraftState.Trigger.Takeoff);
                                _ = SendToDiscord($"Aircraft ID: {aircarft.Id} (Pilot {aircarft.Pilot}) took off from {airfield.Name}");
                                continue;
                            }
                        }

                        foreach (var runway in airfield.Runways)
                        {
                            if (runway.Area.GetBounds().Contains(aircarft.GeoPoint))
                            {
                                if (aircarft.Altitude <= airfield.Altitude + 10 && airfield.Aircraft[aircarft.Id].CurrentState != AircraftState.State.Outbound)
                                {
                                    airfield.Aircraft[aircarft.Id].Update(AircraftState.Trigger.EnterRunway);
                                    _ = SendToDiscord($"Aircraft ID: {aircarft.Id} (Pilot {aircarft.Pilot}) entered runway {runway.Name} at {airfield.Name}");
                                    continue;
                                } else
                                {
                                    airfield.Aircraft[aircarft.Id].Update(AircraftState.Trigger.Land);
                                    _ = SendToDiscord($"Aircraft ID: {aircarft.Id} (Pilot {aircarft.Pilot}) landed at {airfield.Name}");
                                }
                                continue;
                            }
                        }

                        foreach (var navigationPoint in airfield.TaxiwayPoints)
                        {
                            if (navigationPoint.Position.GetBounds().Contains(aircarft.GeoPoint))
                            {
                                if (airfield.Aircraft[aircarft.Id].CurrentState == AircraftState.State.OnRunway)
                                {
                                    _ = SendToDiscord($"Aircraft ID: {aircarft.Id} (Pilot {aircarft.Pilot}) entered {navigationPoint.Name} from runway at {airfield.Name}");
                                } else if (airfield.Aircraft[aircarft.Id].CurrentState == AircraftState.State.OnGround)
                                {
                                    _ = SendToDiscord($"Aircraft ID: {aircarft.Id} (Pilot {aircarft.Pilot}) has taxied to {navigationPoint.Name} at {airfield.Name}");
                                }
                                airfield.Aircraft[aircarft.Id].Update(AircraftState.Trigger.EnterTaxiwayRunwayBoundary);
                            }
                        }
                    }
                }
            }
        }

        private async Task SendToDiscord(string message)
        {
            Logger.Debug(message);
            await Discord.DiscordClient.SendNavigationPoint(message);
        }
    }

}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Geo;
using NLog;
using RurouniJones.DCS.Airfields.Structure;
using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.SpeechOutput;
using RurouniJones.DCS.OverlordBot.Util;
using Stateless;
using Airfield = RurouniJones.DCS.OverlordBot.Models.Airfield;

namespace RurouniJones.DCS.OverlordBot.Controllers
{
    public class AtcProgressChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private Timer _checkTimer;
        private readonly Player _sender;
        private DateTime _lastInstruction;
        private readonly DateTime _startTime;

        private readonly Airfield _airfield;
        private readonly string _voice;

        public static readonly ConcurrentDictionary<string, AtcProgressChecker> AtcChecks = new ConcurrentDictionary<string, AtcProgressChecker>();

        private readonly List<NavigationPoint> _wayPoints;
        
        private StateMachine<State, Trigger> _atcState;

        public State CurrentState => _atcState.State;
        public Runway Destination => (Runway) _wayPoints.Last();

        public enum State
        {
            Parked, // Player is currently parked. This is the initial state for someone calling in that they are ready to taxi
            TaxiToRunway, // Player is currently taxiing
            HoldingShort, // Player is holding short. Currently we only do this for runways, not junctions. Player may be waiting to take off or cross
            LinedUpAndWaiting, // Lined up and waiting for take-off clearance
            Rolling, // started their take-off roll
            Outbound, // They are airborne from the runway and outbound
            Flying, // They are flying and not under ATC control. This is also the initial state for players calling into approach for landing
            Inbound, // They are now inbound and under ATC control
            Base, // Started their base leg to the "Initial"
            Final, // Started their final leg
            ShortFinal, // Entered Short final
            Landed, // Have landed on the runway
            TaxiToRamp, // Have left the runway and are taxiing to the ramp
            Aborted // Aborted their approach
        }

        public enum Trigger
        {
            StartTaxi,
            HoldShort,
            LineUpAndWait,
            StartTakeoffRoll,
            Takeoff,
            LeaveAtcControl,
            StartInbound,
            TurnBase,
            TurnFinal,
            EnterShortFinal,
            Touchdown,
            LeaveRunway,
            Abort
        }

        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private readonly int _checkInterval = 1000;
        private readonly int _transmissionInterval = 20000;

        private NavigationPoint _currentTaxiPoint;

        private string _previousId;
        private static List<State> _holdShortReasons = new List<State>() {State.Base, State.Final, State.ShortFinal};

        public AtcProgressChecker(Player sender, Airfield airfield, string voice, List<NavigationPoint> wayPoints,
            ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _airfield = airfield;
            _voice = voice;
            _wayPoints = wayPoints;
            _responseQueue = responseQueue;
            _lastInstruction = DateTime.Now;
            _startTime = DateTime.Now;

            if (!_airfield.ControlledAircraft.ContainsKey(_sender.Id)) return;
            _airfield.ControlledAircraft[_sender.Id].Stop();
            _airfield.ControlledAircraft.TryRemove(_sender.Id, out _);
        }

        public void CalledInbound()
        {
            _atcState = new StateMachine<State, Trigger>(State.Flying);
            ConfigureStateMachine();

            _atcState.FireAsync(Trigger.StartInbound);
            
            _airfield.ControlledAircraft[_sender.Id] = this;
            AtcChecks[_sender.Id] = this;
        }

        public void CalledTaxi()
        {
            Logger.Info($"{_sender.Id} - {_sender.Callsign} Starting Taxi monitoring");
            _atcState = new StateMachine<State, Trigger>(State.Parked);
            ConfigureStateMachine();

            _atcState.FireAsync(Trigger.StartTaxi);
            
            _airfield.ControlledAircraft[_sender.Id] = this;
            AtcChecks[_sender.Id] = this;
        }

        private void ConfigureStateMachine()
        {
            _atcState.Configure(State.Parked)
                .Permit(Trigger.StartTaxi, State.TaxiToRunway);

            _atcState.Configure(State.TaxiToRunway)
                .OnEntryFromAsync(Trigger.StartTaxi, StartTaxi)
                .Permit(Trigger.HoldShort, State.HoldingShort)
                .Permit(Trigger.LineUpAndWait, State.LinedUpAndWaiting)
                .Permit(Trigger.StartTakeoffRoll, State.Rolling);

            _atcState.Configure(State.HoldingShort)
                .Permit(Trigger.LineUpAndWait, State.LinedUpAndWaiting)
                .Permit(Trigger.StartTakeoffRoll, State.Rolling);

            _atcState.Configure(State.LinedUpAndWaiting)
                .Permit(Trigger.StartTakeoffRoll, State.Rolling);

            _atcState.Configure(State.Rolling)
                .Permit(Trigger.Takeoff, State.Outbound);

            _atcState.Configure(State.Outbound)
                .Permit(Trigger.LeaveAtcControl, State.Flying);

            _atcState.Configure(State.Flying)
                .Permit(Trigger.StartInbound, State.Inbound);

            _atcState.Configure(State.Inbound)
                .OnEntryFromAsync(Trigger.StartInbound, StartInbound)
                .Permit(Trigger.TurnBase, State.Base)
                .PermitReentry(Trigger.StartInbound);

            _atcState.Configure(State.Base)
                .OnEntryFromAsync(Trigger.TurnBase, TurnBase)
                .Permit(Trigger.TurnFinal, State.Final);

            _atcState.Configure(State.Final)
                .OnEntryFromAsync(Trigger.TurnFinal, TurnFinal)
                .Permit(Trigger.EnterShortFinal, State.ShortFinal);

            _atcState.Configure(State.ShortFinal)
                .OnEntryFromAsync(Trigger.EnterShortFinal, EnterShortFinal)
                .Permit(Trigger.Touchdown, State.Landed);

            _atcState.Configure(State.Landed)
                .OnEntryFromAsync(Trigger.Touchdown, Touchdown)
                .Permit(Trigger.LeaveRunway, State.TaxiToRamp);

            _atcState.Configure(State.TaxiToRamp)
                .OnEntryFromAsync(Trigger.LeaveRunway, LeftRunway);

            _atcState.OnUnhandledTrigger(BadTransition);
        }

        private async Task UpdatePlayerData()
        {
            _previousId = _sender.Id;
            await GameQuerier.PopulatePilotData(_sender);
        }

        private bool IsPlayerDeleted()
        {
            // If the caller does not exist any more or the ID has been reused for a different object then cancel the check.
            if (_sender.Id != null && _sender.Id == _previousId) return false;
            _sender.Id = "DELETED";
            Logger.Debug(
                $"{_previousId} - {_sender.Callsign}: Stopping Approach Progress Check. CallerId changed, New: {_sender.Id} , Old: {_previousId}.");
            return true;
        }

        private void BadTransition(State state, Trigger trigger, ICollection<string> u)
        {
            string strings = null;
            if(u != null)
              strings = string.Join(", ", u.ToList());
            Logger.Error($"{_sender.Id} - {_sender.Callsign} Bad Transition: {state}, {trigger}, {strings}");
            Stop(true);
        }

        #region Triggers

        /// <summary>
        /// Starts a periodic check by re-creating a timer. This makes sure that there is only
        /// ever one timer active and that it is properly disposed of and re-created.
        ///
        /// All Trigger methods should use this method to setup checks.
        /// </summary>
        /// <param name="code">Code to run before starting the next periodic check. e.g. Send a transmission</param>
        /// <param name="periodicCheck">The code that periodically be called by the timer</param>
        /// <returns>A Task</returns>
        private async Task StartPeriodicCheck(Func<Task> code, Func<Task> periodicCheck)
        {
            _checkTimer?.Stop();
            await code();
            _checkTimer = new Timer(_checkInterval);
            _checkTimer.Elapsed += async (s, e) => await periodicCheck();
            _checkTimer.Start();
        }

        private async Task StartInbound()
        {
            await StartPeriodicCheck(async () => await Task.Run(() => 
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Starting inbound, current waypoint {_wayPoints[0].Name}");
                    _lastInstruction = DateTime.Now;
                    _wayPoints.RemoveAt(0);
                }),
                CheckInbound);
        }

        private async Task TurnBase()
        {
            await StartPeriodicCheck(async () =>
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Turning base");
                    _wayPoints.RemoveAt(0);
                    await TransmitHeadingToNextWaypoint(", descend and maintain 1000, reduce speed your discretion");
                },
                async() => await IsApproachingNextWayPoint(1.35, Trigger.TurnFinal));
        }

        private async Task TurnFinal()
        {
            await StartPeriodicCheck(async () =>
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Turning final");
                    _wayPoints.RemoveAt(0);
                    var runway = (Runway) _wayPoints.First();
                    await SendMessage($"turn final {runway.Name}");
                },
                async() => await IsApproachingNextWayPoint(1.5, Trigger.EnterShortFinal));
        }

        private async Task EnterShortFinal()
        {
            await StartPeriodicCheck(async () =>
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: entering short final");
                    await SendMessage($"Check gear, land {_wayPoints.First().Name} at your discretion");
                },
                IsTouchedDown);
        }

        private async Task Touchdown()
        {

            await StartPeriodicCheck(async () => await Task.Run(() =>
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: touched down");
                }),
                IsExitedRunway);
        }

        private async Task LeftRunway()
        {
            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Left runway");
            await SendMessage($"Taxi to parking area at your discretion");
            await UpdatePlayersHoldingShort();
            Stop();
        }

        
        private async Task StartTaxi()
        {

            await StartPeriodicCheck(async () => await Task.Run(() =>
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Started Taxi");
                }),
                CheckTaxiProgress);
        }

        #endregion

        #region Periodic Checks

        /// <summary>
        /// Performs a check and makes sure that the player object has been updated before running
        /// the lambda containing the check code. Also stops the checker if the player has been
        /// deleted
        /// </summary>
        /// <param name="check">The code that does the actual checking</param>
        /// <returns></returns>
        private async Task PerformCheck(Func<Task> check)
        {
            Logger.Trace($"{_sender.Id} - {_sender.Callsign}: Checking Progress. Current state is {_atcState.State}");
            await UpdatePlayerData();
            if (await Task.Run(IsPlayerDeleted)) {
                Stop();
                return;
            }
            await check();
        }

        private async Task CheckInbound()
        {
            await PerformCheck(async () =>
            {
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Inbound Progress Check");
                var nextWayPoint = _wayPoints.First();

                Logger.Debug(
                    $"{_sender.Id} is {nextWayPoint.DistanceTo(_sender.Position.Coordinate)} KM from {nextWayPoint.Name}");

                // THINK ABOUT: Change this fixed value to a relative ratio based on the distances?
                if (nextWayPoint.DistanceTo(_sender.Position.Coordinate) < 1)
                {
                    if (nextWayPoint.Name.Contains("Entry"))
                    {
                        await _atcState.FireAsync(Trigger.TurnBase);
                    }
                    else
                    {
                        await _atcState.FireAsync(Trigger.StartInbound);
                    }

                    return;
                }

                // No point with last minute instructions when we are sending them to initial soon anyway
                if (nextWayPoint.DistanceTo(_sender.Position.Coordinate) < 2.5)
                {
                    return;
                }

                if (_sender.Heading == null)
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: heading was null");
                    return;
                }

                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Time between two transmissions ok");

                var sH = (int) _sender.Heading;
                var wH = (int) Geospatial.BearingTo(_sender.Position.Coordinate,
                    new Coordinate(nextWayPoint.Latitude, nextWayPoint.Longitude));

                var headingDiff = Math.Min((wH - sH) < 0 ? wH - sH + 360 : wH - sH,
                    (sH - wH) < 0 ? sH - wH + 360 : sH - wH);

                Logger.Debug(
                    $"{_sender.Id} - {_sender.Callsign}: Headings: Waypoint {wH}, Player {sH}, diff {headingDiff}");

                Logger.Debug(
                    $"{_sender.Id} - {_sender.Callsign}: {(DateTime.Now - _lastInstruction).TotalSeconds} since last transmission");
                if ((DateTime.Now - _lastInstruction).TotalMilliseconds < _transmissionInterval)
                {
                    Logger.Debug(
                        $"{_sender.Id} - {_sender.Callsign}: Time between two transmissions too low, returning");
                    return;
                }

                if (headingDiff <= 5)
                    return;

                var magneticHeading = Regex.Replace(Geospatial.TrueToMagnetic(_sender.Position, wH).ToString("000"),
                    "\\d{1}", " $0");
                _lastInstruction = DateTime.Now;
                await SendMessage($"fly heading {magneticHeading}");
            });
        }

        private async Task IsApproachingNextWayPoint(double triggerDistance, Trigger trigger)
        {
            await PerformCheck(async () =>
            {
                var distance = _wayPoints.First().DistanceTo(_sender.Position.Coordinate);
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Checking distance to next waypoint. {distance} KM");
                if ( distance < triggerDistance)
                {
                    await _atcState.FireAsync(trigger);
                }
            });
        }

        private async Task IsTouchedDown()
        {
            await PerformCheck(async () =>
            {
                Logger.Debug(
                    $"{_sender.Id} - {_sender.Callsign}: Checking touched down. Player altitude {_sender.Altitude}, Airfield altitude {_airfield.Altitude}");
                if (_sender.Altitude <= _airfield.Altitude + 5)
                    await _atcState.FireAsync(Trigger.Touchdown);
            });
        }

        private async Task IsExitedRunway()
        {
            await PerformCheck(async () =>
            {
                var closestPoint = _airfield.TaxiPoints
                    .OrderBy(taxiPoint => taxiPoint.DistanceTo(_sender.Position.Coordinate))
                    .First();

                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Checking if player has left runway. Nearest NavigationPoint is {closestPoint.Name}");

                if (!_airfield.RunwayNodes[Destination].Contains(closestPoint))
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Left runway");
                    _checkTimer.Stop();
                    await _atcState.FireAsync(Trigger.LeaveRunway);
                }
            });
        }

        private async Task CheckTaxiProgress()
        {
            await PerformCheck(async () =>
            {
                var closestPoint = _wayPoints.OrderBy(taxiPoint => taxiPoint.DistanceTo(_sender.Position.Coordinate))
                    .First();

                // If this is true then we don't need to say the same commands again etc.
                if (_currentTaxiPoint == closestPoint)
                    return;

                // We want to get rid of all the previous TaxiPoints in the list. We do this instead of just getting rid of the first in case
                // somehow the pilot manage to skip a TaxiPoint by going fast enough that they passed it before the check.
                var index = _wayPoints.IndexOf(_currentTaxiPoint);

                if (index > 1)
                    Logger.Trace($" {_sender.Id} skipped at least one taxi point");

                for (var i = _wayPoints.Count - 1; i >= 0; i--)
                {
                    if (i > index) continue;
                    Logger.Trace($"Removing {_wayPoints[i].Name} from route of {_sender.Id}");
                    _wayPoints.RemoveAt(i);
                }

                _currentTaxiPoint = closestPoint;
                Logger.Debug($"New closest TaxiPoint to {_sender.Id} is {_currentTaxiPoint.Name}");

                if (_currentTaxiPoint is Runway)
                {
                    // Check to see if we have any aircraft on initial or final
                    if (_airfield.ControlledAircraft.Values.Any(x => _holdShortReasons.Contains(x.CurrentState) && x.Destination == _wayPoints.First()))
                    {
                        await SendMessage($"Hold short {_currentTaxiPoint.Name}");
                        await _atcState.FireAsync(Trigger.HoldShort);
                    }
                    else
                    {
                        if (_wayPoints.Count == 1)
                        {
                            await SendMessage($"Take-off {_currentTaxiPoint.Name} at your discretion");
                            await _atcState.FireAsync(Trigger.LineUpAndWait);
                        }
                        else
                        {
                            // If we have reached this bit in the code then the current taxi point is a runway that is not the terminus of the route
                            // so tell the player they are good to cross.
                            await SendMessage($"cross {_currentTaxiPoint.Name} at your discretion");
                        }
                    }
                }
            });
        }

        #endregion

        #region Decisions

        private async Task UpdatePlayersHoldingShort()
        {
            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Looking for players holding short");
        }

        #endregion

        public void Stop(bool error = false)
        {
            var id = _sender.Id.Equals("DELETED") ? _previousId : _sender.Id;
            Logger.Debug($"{id} - {_sender.Callsign}: Stopping ATC progress check. Error is {error}");
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
            _checkTimer = null;
            _airfield.ControlledAircraft.TryRemove(id, out _);
            AtcChecks.TryRemove(id, out _);
        }

        private async Task TransmitHeadingToNextWaypoint(string comment)
        {
            var nextWayPoint = _wayPoints.First();

            var wH = (int) Geospatial.BearingTo(_sender.Position.Coordinate,
                new Coordinate(nextWayPoint.Latitude, nextWayPoint.Longitude));

            var magneticHeading = Regex.Replace(Geospatial.TrueToMagnetic(_sender.Position, wH).ToString("000"),
                "\\d{1}", " $0");

            _lastInstruction = DateTime.Now;
            await SendMessage($"fly heading {magneticHeading} for {nextWayPoint.Name} {comment}");
        }

        private async Task SendMessage(string message)
        {
            var response = $"{_sender.Callsign}, {message}"; 

            var ssmlResponse =
                $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>";

            var audioData = await Speaker.CreateResponse(ssmlResponse);

            if (audioData == null)
            {
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}:| First Synthesis failed, trying again");
                audioData = await Task.Run(() => Speaker.CreateResponse(ssmlResponse));
            }

            if (audioData != null)
            {
                Logger.Info($"{_sender.Id} - {_sender.Callsign}: Outgoing Transmission: {response}");
                _responseQueue.Enqueue(audioData);
            }
        }
    }
}

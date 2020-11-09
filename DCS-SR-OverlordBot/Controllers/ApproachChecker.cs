using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Xaml;
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
    public class ApproachChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private Timer _checkTimer;
        private readonly Player _sender;
        private DateTime _lastInstruction;
        private DateTime _startTime;

        private readonly Airfield _airfield;
        private readonly string _voice;

        public static readonly ConcurrentDictionary<string, ApproachChecker> ApproachChecks = new ConcurrentDictionary<string, ApproachChecker>();

        private readonly List<NavigationPoint> _wayPoints;
        
        private readonly StateMachine<State, Trigger> _approachState;

        public State CurrentState => _approachState.State;
        public NavigationPoint Destination => _wayPoints.Last();

        public enum State {Flying, Inbound, Base, Final, ShortFinal, Landed, Taxi, Aborted}
        public enum Trigger { StartInbound, TurnBase, TurnFinal, EnterShortFinal, Touchdown, LeaveRunway, Abort }

        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private readonly int _checkInterval = 1000;
        private readonly int _transmissionInterval = 20000;

        private string _previousId;

        public ApproachChecker(Player sender, Airfield airfield, string voice, List<NavigationPoint> wayPoints,
            ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _airfield = airfield;
            _voice = voice;
            _wayPoints = wayPoints;
            _responseQueue = responseQueue;
            _lastInstruction = DateTime.Now;
            _startTime = DateTime.Now;

            if (_airfield.ApproachingAircraft.ContainsKey(_sender.Id))
            {
                _airfield.ApproachingAircraft[_sender.Id].Stop();
                _airfield.ApproachingAircraft.TryRemove(_sender.Id, out _);
            }

            _approachState = new StateMachine<State, Trigger>(State.Flying);
            ConfigureStateMachine();

            _approachState.FireAsync(Trigger.StartInbound);
            
            _airfield.ApproachingAircraft[_sender.Id] = this;
            ApproachChecks[_sender.Id] = this;
        }

        private void ConfigureStateMachine()
        {
            _approachState.Configure(State.Flying)
                .Permit(Trigger.StartInbound, State.Inbound);

            _approachState.Configure(State.Inbound)
                .OnEntryFromAsync(Trigger.StartInbound, StartInbound)
                .Permit(Trigger.TurnBase, State.Base)
                .PermitReentry(Trigger.StartInbound);

            _approachState.Configure(State.Base)
                .OnEntryFromAsync(Trigger.TurnBase, TurnBase)
                .Permit(Trigger.TurnFinal, State.Final);

            _approachState.Configure(State.Final)
                .OnEntryFromAsync(Trigger.TurnFinal, TurnFinal)
                .Permit(Trigger.EnterShortFinal, State.ShortFinal);

            _approachState.Configure(State.ShortFinal)
                .OnEntryFromAsync(Trigger.EnterShortFinal, EnterShortFinal)
                .Permit(Trigger.Touchdown, State.Landed);

            _approachState.Configure(State.Landed)
                .OnEntryFromAsync(Trigger.TurnFinal, Touchdown)
                .Permit(Trigger.LeaveRunway, State.Taxi);

            _approachState.OnUnhandledTrigger((s, t, u) => Stop());
        }

        private async Task CheckDuration()
        {
            if ((DateTime.Now - _startTime).TotalMinutes > 10)
            {
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Inbound for more than 10 minutes. Stopping check.");
                Stop();
            }
        }

        private async Task StartInbound()
        {
            await Task.Run(() =>
            {
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Starting inbound, current waypoint {_wayPoints[0].Name}");
                _lastInstruction = DateTime.Now;
                _checkTimer?.Stop();
                _wayPoints.RemoveAt(0);
                _checkTimer = new Timer(_checkInterval);
                _checkTimer.Elapsed += async (s, e) => await CheckInboundAsync();
                _checkTimer.Start();
            });
        }

        private async Task TurnBase()
        {
            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Turning base");
            _checkTimer.Stop();
            _wayPoints.RemoveAt(0);
            _checkTimer = new Timer(_checkInterval);
            
            _checkTimer.Elapsed += async (s, e) => await FireTriggerOnNextWayPoint(1.35, Trigger.TurnFinal);
            _checkTimer.Start();
            await TransmitHeadingToNextWaypoint("descend and maintain 1000, reduce speed your discretion");
        }

        private async Task TurnFinal()
        {
            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Turning final");
            _checkTimer.Stop();
            _wayPoints.RemoveAt(0);
            _checkTimer = new Timer(_checkInterval);
            
            _checkTimer.Elapsed += async (s, e) => await FireTriggerOnNextWayPoint(1.5, Trigger.EnterShortFinal);
            _checkTimer.Start();
            var runway = (Runway) _wayPoints.First();
            await SendMessage($"turn final {runway.Name}");
        }

        private async Task EnterShortFinal()
        {
            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: entering short final");
            _checkTimer.Stop();
            await SendMessage($"Check gear, land {_wayPoints.First().Name} at your discretion");
            _checkTimer.Elapsed += async (s, e) => await HasTouchedDown();
            _checkTimer.Start();
        }

        private async Task HasTouchedDown()
        {
            if (_sender.Altitude < (_airfield.Altitude + 3))
                await _approachState.FireAsync(Trigger.Touchdown);
        }

        private async Task Touchdown()
        {
            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: touched down");
            _checkTimer.Stop();
            _checkTimer.Elapsed += async (s, e) => await IsExitedRunway();
            _checkTimer.Start();
        }

        private async Task IsExitedRunway()
        {
            var closestPoint = _airfield.TaxiPoints.OrderBy(taxiPoint => taxiPoint.DistanceTo(_sender.Position.Coordinate))
                .First();
            if (!closestPoint.Name.Contains("Runway"))
            {
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Left runway");
                _checkTimer.Stop();
                Stop();
                await _approachState.FireAsync(Trigger.LeaveRunway);
            }
        }

        public void Stop()
        {
            var id = _sender.Id.Equals("DELETED") ? _previousId : _sender.Id;
            Logger.Debug($"{id} - {_sender.Callsign}: Stopping approach progress check");
            _checkTimer.Stop();
            _checkTimer.Dispose();
            _airfield.ApproachingAircraft.TryRemove(id, out _);
            ApproachChecks.TryRemove(id, out _);
        }

        private async Task FireTriggerOnNextWayPoint(double distance, Trigger trigger)
        {
            await CheckDuration();
            if (await IsPlayerDeleted())
                return;

            if (_wayPoints.First().DistanceTo(_sender.Position.Coordinate) < distance)
            {
                await _approachState.FireAsync(trigger);
            }
        }

        private async Task<bool> IsPlayerDeleted()
        {
            _previousId = _sender.Id;
            await GameQuerier.PopulatePilotData(_sender);

            // If the caller does not exist any more or the ID has been reused for a different object then cancel the check.
            if (_sender.Id != null && _sender.Id == _previousId) return false;
            _sender.Id = "DELETED";
            Logger.Debug(
                $"{_previousId} - {_sender.Callsign}: Stopping Approach Progress Check. CallerId changed, New: {_sender.Id} , Old: {_previousId}.");
            Stop();
            return true;
        }

        private async Task CheckInboundAsync()
        {
            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Inbound Progress Check");
            await CheckDuration();
            if (await IsPlayerDeleted())
                return;

            var nextWayPoint = _wayPoints.First();

            Logger.Debug($"{_sender.Id} is {nextWayPoint.DistanceTo(_sender.Position.Coordinate)} KM from {nextWayPoint.Name}");


            // THINK ABOUT: Change this fixed value to a relative ratio based on the distances?
            if (nextWayPoint.DistanceTo(_sender.Position.Coordinate) < 1)
            {
                if (nextWayPoint.Name.Contains("Entry"))
                {
                    await _approachState.FireAsync(Trigger.TurnBase);

                }
                else
                {
                    await _approachState.FireAsync(Trigger.StartInbound);

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

            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: {(DateTime.Now - _lastInstruction).TotalSeconds} since last transmission");
            if ((DateTime.Now - _lastInstruction).TotalMilliseconds < _transmissionInterval)
            {
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Time between two transmissions too low, returning");
                return;
            }

            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Time between two transmissions ok");

            var sH = (int) _sender.Heading;
            var wH = (int) Geospatial.BearingTo(_sender.Position.Coordinate,
                new Coordinate(nextWayPoint.Latitude, nextWayPoint.Longitude));

            var headingDiff = Math.Min((wH - sH) < 0 ? wH - sH + 360 : wH - sH,
                (sH - wH) < 0 ? sH - wH + 360 : sH - wH);

            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Headings: Waypoint {wH}, Player {sH}, diff {headingDiff}");

            if (headingDiff <= 5) 
                return;

            var magneticHeading = Regex.Replace(Geospatial.TrueToMagnetic(_sender.Position, wH).ToString("000"),
                "\\d{1}", " $0");
            _lastInstruction = DateTime.Now;
            await SendMessage($"fly heading {magneticHeading}");
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

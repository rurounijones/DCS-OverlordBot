using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
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
    public class ApproachChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private Timer _checkTimer;
        private readonly Player _sender;
        private DateTime _lastInstruction;


        private readonly Airfield _airfield;
        private readonly string _voice;

        private readonly List<NavigationPoint> _wayPoints;
        
        private readonly StateMachine<State, Trigger> _approachState;

        public enum State {Flying, Inbound, Base, Final, ShortFinal}
        public enum Trigger { StartInbound, TurnBase, TurnFinal, EnterShortFinal}

        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private readonly int _checkInterval = 1000; //new TimeSpan(0, 0, 0, 1).Milliseconds;
        private readonly int _transmissionInterval = 20000; //new TimeSpan(0, 0, 0, 1).Milliseconds;

        public ApproachChecker(Player sender, Airfield airfield, string voice, List<NavigationPoint> wayPoints,
            ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _airfield = airfield;
            _voice = voice;
            _wayPoints = wayPoints;
            _responseQueue = responseQueue;
            _lastInstruction = DateTime.Now; // - new TimeSpan(0,0,0,10); // Fudge it since transmission takes time

            if (_airfield.ApproachingAircraft.ContainsKey(_sender.Id))
            {
                _airfield.ApproachingAircraft[_sender.Id].Stop();
                _airfield.ApproachingAircraft.TryRemove(_sender.Id, out _);
            }

            _approachState = new StateMachine<State, Trigger>(State.Flying);
            ConfigureStateMachine();

            _approachState.FireAsync(Trigger.StartInbound);
            
            _airfield.ApproachingAircraft[_sender.Id] = this;
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
                .OnEntryFromAsync(Trigger.EnterShortFinal, EnterShortFinal);
        }

        private async Task StartInbound()
        {
            await Task.Run(() =>
            {
                Logger.Debug($"{_sender.Id} Starting inbound, current waypoint {_wayPoints[0].Name}");
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
            Logger.Debug($"{_sender.Id} Turning base");
            _checkTimer.Stop();
            _wayPoints.RemoveAt(0);
            _checkTimer = new Timer(_checkInterval);
            
            _checkTimer.Elapsed += async (s, e) => await FireTriggerOnNextWayPoint(1.35, Trigger.TurnFinal);
            _checkTimer.Start();
            await TransmitHeadingToNextWaypoint();
        }

        private async Task TurnFinal()
        {
            Logger.Debug($"{_sender.Id} Turning final");
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
            Logger.Debug($"{_sender.Id} entering short final");
            _checkTimer.Stop();
            await SendMessage($"Check gear, land {_wayPoints.First().Name} at your discretion");
            await Task.Run(Stop);
        }

        private void Stop()
        {
            Logger.Debug($"Stopping approach progress check for {_sender.Id}");
            _checkTimer.Stop();
            _checkTimer.Close();
            _airfield.ApproachingAircraft.TryRemove(_sender.Id, out _);
        }

        private async Task FireTriggerOnNextWayPoint(double distance, Trigger trigger)
        {
            if (await IsPlayerDeleted())
                return;

            if (_wayPoints.First().DistanceTo(_sender.Position.Coordinate) < distance)
            {
                await _approachState.FireAsync(trigger);
            }
        }

        private async Task<bool> IsPlayerDeleted()
        {
            var previousId = _sender.Id;
            await GameQuerier.PopulatePilotData(_sender);

            // If the caller does not exist any more or the ID has been reused for a different object then cancel the check.
            if (_sender.Id != null && _sender.Id == previousId) return false;
            _sender.Id = "DELETED";
            Logger.Debug(
                $"Stopping Approach Progress Check. CallerId changed, New: {_sender.Id} , Old: {previousId}.");
            Stop();
            return true;
        }

        private async Task CheckInboundAsync()
        {
            Logger.Debug($"Inbound Progress Check for {_sender.Id}");
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
                Logger.Debug($"{_sender.Id} heading was null");
                return;
            }

            Logger.Debug($"{_sender.Id}. {(DateTime.Now - _lastInstruction).TotalSeconds} since last transmission");
            if ((DateTime.Now - _lastInstruction).TotalMilliseconds < _transmissionInterval)
            {
                Logger.Debug($"Time between two transmissions too low, returning");
                return;
            }

            Logger.Debug($"Time between two transmissions ok");

            var sH = (int) _sender.Heading;
            var wH = (int) Geospatial.BearingTo(_sender.Position.Coordinate,
                new Coordinate(nextWayPoint.Latitude, nextWayPoint.Longitude));

            var headingDiff = Math.Min((wH - sH) < 0 ? wH - sH + 360 : wH - sH,
                (sH - wH) < 0 ? sH - wH + 360 : sH - wH);

            Logger.Debug($"Headings: Waypoint {wH}, Player {sH}, diff {headingDiff}");

            if (headingDiff <= 5) 
                return;

            var magneticHeading = Regex.Replace(Geospatial.TrueToMagnetic(_sender.Position, wH).ToString("000"),
                "\\d{1}", " $0");
            _lastInstruction = DateTime.Now;
            await SendMessage($"fly heading {magneticHeading}");
        }

        private async Task TransmitHeadingToNextWaypoint()
        {
            var nextWayPoint = _wayPoints.First();

            var wH = (int) Geospatial.BearingTo(_sender.Position.Coordinate,
                new Coordinate(nextWayPoint.Latitude, nextWayPoint.Longitude));

            var magneticHeading = Regex.Replace(Geospatial.TrueToMagnetic(_sender.Position, wH).ToString("000"),
                "\\d{1}", " $0");

            _lastInstruction = DateTime.Now;
            await SendMessage($"fly heading {magneticHeading} for {nextWayPoint.Name}");
        }

        private async Task SendMessage(string message)
        {
            var name = AirbasePronouncer.PronounceAirbase(_airfield.Name);
            var response =
                $"{_sender.Callsign}, {name} approach, {message}"; 

            var ssmlResponse =
                $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>";

            var audioData = await Speaker.CreateResponse(ssmlResponse);

            if (audioData != null)
            {
                Logger.Info($"Outgoing Transmission: {response}");
                _responseQueue.Enqueue(audioData);
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> _startInboundTrigger;
        private StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> _turnInitialTrigger;
        private StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> _turnFinalTrigger;
        private StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> _enterShortFinalTrigger;

        
        public enum State {Flying, Inbound, Initial, Final, ShortFinal}
        public enum Trigger { StartInbound, TurnInitial, TurnFinal, EnterShortFinal}

        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private readonly int _checkInterval = new TimeSpan(0, 0, 0, 1).Seconds;

        public ApproachChecker(Player sender, Airfield airfield, string voice, List<NavigationPoint> wayPoints,
            ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _airfield = airfield;
            _voice = voice;
            _wayPoints = wayPoints;
            _responseQueue = responseQueue;
            _lastInstruction = DateTime.Now;

            _approachState = new StateMachine<State, Trigger>(State.Flying);
            ConfigureStateMachine();

            _approachState.FireAsync(_startInboundTrigger, wayPoints.First());

            if (_airfield.ApproachingAircraft.ContainsKey(_sender.Id))
            {
                _airfield.ApproachingAircraft[_sender.Id].Stop();
                _airfield.ApproachingAircraft.TryRemove(_sender.Id, out _);
            }
            
            _airfield.ApproachingAircraft[_sender.Id] = this;
        }

        private void ConfigureStateMachine()
        {
            _startInboundTrigger = _approachState.SetTriggerParameters<NavigationPoint>(Trigger.StartInbound);
            _turnInitialTrigger = _approachState.SetTriggerParameters<NavigationPoint>(Trigger.TurnInitial);
            _turnFinalTrigger = _approachState.SetTriggerParameters<NavigationPoint>(Trigger.TurnFinal);
            _enterShortFinalTrigger = _approachState.SetTriggerParameters<NavigationPoint>(Trigger.EnterShortFinal);

            _approachState.Configure(State.Flying)
                .Permit(Trigger.StartInbound, State.Inbound);

            _approachState.Configure(State.Inbound)
                .OnEntryFromAsync(_startInboundTrigger, StartInbound)
                .Permit(Trigger.TurnInitial, State.Initial);

            _approachState.Configure(State.Initial)
                .OnEntryFromAsync(_turnInitialTrigger, TurnInitial)
                .Permit(Trigger.TurnFinal, State.Final);

            _approachState.Configure(State.Final)
                .OnEntryFromAsync(_turnFinalTrigger, TurnFinal)
                .Permit(Trigger.EnterShortFinal, State.ShortFinal);

            _approachState.Configure(State.ShortFinal)
                .OnEntryFromAsync(_enterShortFinalTrigger, EnterShortFinal);
        }

        private async Task StartInbound(NavigationPoint startPoint)
        {
            await Task.Run(() =>
            {
                _checkTimer = new Timer(_checkInterval);
                _checkTimer.Elapsed += async (s, e) => await CheckInboundAsync(startPoint);
                _checkTimer.Start();
            });
        }

        private async Task TurnInitial(NavigationPoint entryPoint)
        {
            _checkTimer.Stop();
            _checkTimer = new Timer(_checkInterval);
            
            var nextWayPoint = _wayPoints[_wayPoints.IndexOf(entryPoint) + 1];

            _checkTimer.Elapsed += async (s, e) => await FireTriggerOnNextWayPoint(nextWayPoint, 0.5, _turnFinalTrigger);
            _checkTimer.Start();
            await TransmitHeadingToNextWaypoint(entryPoint);
        }

        private async Task TurnFinal(NavigationPoint initialPoint)
        {
            _checkTimer.Stop();
            _checkTimer = new Timer(_checkInterval);
            
            var nextWayPoint = _wayPoints[_wayPoints.IndexOf(initialPoint) + 1];

            _checkTimer.Elapsed += async (s, e) => await FireTriggerOnNextWayPoint(nextWayPoint, 0.5, _enterShortFinalTrigger);
            _checkTimer.Start();
            await TransmitHeadingToNextWaypoint(initialPoint);
        }

        private async Task EnterShortFinal(NavigationPoint runway)
        {
            _checkTimer.Stop();
            await SendMessage($"Check gear, land {runway.Name} at your discretion");
            await Task.Run(Stop);
        }

        private void Stop()
        {
            Logger.Debug($"Stopping approach progress check for {_sender.Id}");
            _checkTimer.Stop();
            _checkTimer.Close();
            _airfield.ApproachingAircraft.TryRemove(_sender.Id, out _);
        }

        private async Task FireTriggerOnNextWayPoint(NavigationPoint nextWayPoint, double distance, StateMachine<State, Trigger>.TriggerWithParameters<NavigationPoint> trigger)
        {
            if (await IsPlayerDeleted())
                return;

            if (nextWayPoint.DistanceTo(_sender.Position.Coordinate) < distance)
            {
                await _approachState.FireAsync(trigger, nextWayPoint);
            }
        }


        private async Task<bool> IsPlayerDeleted()
        {
            var previousId = _sender.Id;
            await GameQuerier.PopulatePilotData(_sender);

            // If the caller does not exist any more or the ID has been reused for a different object then cancel the check.
            if (_sender.Id == null || _sender.Id != previousId)
            {
                _sender.Id = "DELETED";
                Logger.Debug(
                    $"Stopping Approach Progress Check. CallerId changed, New: {_sender.Id} , Old: {previousId}.");
                Stop();
                return true;
            }

            return false;
        }

        private async Task CheckInboundAsync(NavigationPoint currentWayPoint)
        {
            if (await IsPlayerDeleted())
                return;

            var nextWayPoint = _wayPoints[_wayPoints.IndexOf(currentWayPoint) + 1];

            // THINK ABOUT: Change this fixed value to a relative ratio based on the distances?
            if (nextWayPoint.DistanceTo(_sender.Position.Coordinate) < 1)
            {
                await _approachState.FireAsync(_turnInitialTrigger, nextWayPoint);
                return;
            }

            if (_sender.Heading == null)
            {
                Logger.Debug($"{_sender.Id} heading was null");
                return;
            }

            var sH = (int) _sender.Heading;
            var wH = (int) Geospatial.BearingTo(_sender.Position.Coordinate,
                new Coordinate(nextWayPoint.Latitude, nextWayPoint.Longitude));

            var headingDiff = Math.Min((wH - sH) < 0 ? wH - sH + 360 : wH - sH,
                (sH - wH) < 0 ? sH - wH + 360 : sH - wH);

            if ((headingDiff <= 5) || (DateTime.Now - _lastInstruction).Seconds <= 10) 
                return;
            
            var magneticHeading = Regex.Replace(Geospatial.TrueToMagnetic(_sender.Position, wH).ToString("000"),
                "\\d{1}", " $0");
            _lastInstruction = DateTime.Now;
            await SendMessage($"fly heading {magneticHeading}");
        }

        private async Task TransmitHeadingToNextWaypoint(NavigationPoint currentWaypoint)
        {
            var nextWayPoint = _wayPoints[_wayPoints.IndexOf(currentWaypoint) + 1];

            var wH = (int) Geospatial.BearingTo(_sender.Position.Coordinate,
                new Coordinate(nextWayPoint.Latitude, nextWayPoint.Longitude));

            var magneticHeading = Regex.Replace(Geospatial.TrueToMagnetic(_sender.Position, wH).ToString("000"),
                "\\d{1}", " $0");

            _lastInstruction = DateTime.Now;
            await SendMessage($"fly heading {magneticHeading}");
        }

        private async Task SendMessage(string message)
        {
            var response =
                $"{_sender.Callsign}, {_airfield.Name} approach, {message}"; 

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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using RurouniJones.DCS.Airfields.Structure;
using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.SpeechOutput;
using Airfield = RurouniJones.DCS.OverlordBot.Models.Airfield;

namespace RurouniJones.DCS.OverlordBot.Controllers
{
    public class TaxiProgressChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Timer _checkTimer;
        private readonly Player _sender;

        private readonly Airfield _airfield;
        private readonly string _voice;

        private readonly List<NavigationPoint> _taxiPoints;
        private NavigationPoint _currentTaxiPoint;

        public static readonly ConcurrentDictionary<string, TaxiProgressChecker> TaxiChecks = new ConcurrentDictionary<string, TaxiProgressChecker>();

        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private const double CheckInterval = 1000; // milliseconds

        private string _previousId;

        public TaxiProgressChecker(Player sender, Airfield airfield, string voice, List<NavigationPoint> taxiPoints,
            ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _airfield = airfield;
            _voice = voice;
            _taxiPoints = taxiPoints;
            _responseQueue = responseQueue;

            if (_airfield.TaxiingAircraft.ContainsKey(_sender.Id))
            {
                _airfield.TaxiingAircraft[_sender.Id].Stop();
                _airfield.TaxiingAircraft.TryRemove(_sender.Id, out _);
            }

            // Do once immediately so we get the current taxi-point
            Task.Run(async () => await CheckAsync());

            _checkTimer = new Timer(CheckInterval);
            _checkTimer.Elapsed += async (s, e) => await CheckAsync();

            _checkTimer.Start();

            _airfield.TaxiingAircraft[_sender.Id] = this;
            TaxiChecks[_sender.Id] = this;

        }

        public void Stop()
        {
            var id = _sender.Id.Equals("DELETED") ? _previousId : _sender.Id;
            Logger.Debug($"Stopping taxi progress check for {id}");
            _checkTimer.Stop();
            _checkTimer.Dispose();
            _airfield.TaxiingAircraft.TryRemove(id, out _);
            TaxiChecks.TryRemove(id, out _);
        }

        private async Task CheckAsync()
        {
            try
            {
                Logger.Debug($"Peforming Taxi Progress check for {_sender.Id}");
                _previousId = _sender.Id;
                await GameQuerier.PopulatePilotData(_sender);

                // If the caller does not exist any more or the ID has been reused for a different object
                // then cancel the check.
                if (_sender.Id == null || _sender.Id != _previousId)
                {
                    _sender.Id = "DELETED";
                    Logger.Debug(
                        $"{_sender.Id} - {_sender.Callsign}: Stopping Warning Radius Check. CallerId changed, New: {_sender.Id} , Old: {_previousId}.");
                    Stop();
                    return;
                }

                var closestPoint = _taxiPoints.OrderBy(taxiPoint => taxiPoint.DistanceTo(_sender.Position.Coordinate))
                    .First();

                // If the player is more than 5 miles from the closest TaxiPoint they they are long gone and should not longer
                // be monitored
                if (closestPoint.DistanceTo(_sender.Position.Coordinate) > 5)
                {
                    Stop();
                    return;
                }

                // If this is true then we don't need to say the same commands again etc.
                if (_currentTaxiPoint == closestPoint)
                    return;

                // We want to get rid of all the previous TaxiPoints in the list. We do this instead of just getting rid of the first in case
                // somehow the pilot manage to skip a TaxiPoint by going fast enough that they passed it before the check.
                var index = _taxiPoints.IndexOf(_currentTaxiPoint);

                if (index > 1)
                    Logger.Trace($" {_sender.Id} skipped at least one taxi point");

                for (var i = _taxiPoints.Count - 1; i >= 0; i--)
                {
                    if (i > index) continue;
                    Logger.Trace($"Removing {_taxiPoints[i].Name} from route of {_sender.Id}");
                    _taxiPoints.RemoveAt(i);
                }

                _currentTaxiPoint = closestPoint;
                Logger.Debug($"New closest TaxiPoint to {_sender.Id} is {_currentTaxiPoint.Name}");

                if (_currentTaxiPoint is Runway)
                {
                    using (var activity = Constants.ActivitySource.StartActivity("TaxiProgressChecker.SendTaxiInstruction", ActivityKind.Consumer))
                    {
                        if (_taxiPoints.Count == 1)
                        {
                            Logger.Debug(
                                $"Stopping Taxi Progress Check. {_sender.Id} has reached the end of the taxi route at {_currentTaxiPoint.Name}");

                            // Check to see if we have any aircraft on initial or final
                            if(_airfield.ApproachingAircraft.Values.Any(x => (x.CurrentState == ApproachChecker.State.Base || x.CurrentState == ApproachChecker.State.Final || x.CurrentState == ApproachChecker.State.ShortFinal)
                                                                             && x.Destination == _taxiPoints.First()))
                            {
                                activity?.AddTag("Response", "Hold Short");
                                await SendMessage($"Hold short {_currentTaxiPoint.Name}");
                            }
                            else
                            {
                                activity?.AddTag("Response", "RunwayTakeOff");
                                await SendMessage($"Take-off {_currentTaxiPoint.Name} at your discretion");
                                Stop();
                            }
                        }
                        else
                        {
                            if(_airfield.ApproachingAircraft.Values.Any(x => (x.CurrentState == ApproachChecker.State.Base || x.CurrentState == ApproachChecker.State.Final || x.CurrentState == ApproachChecker.State.ShortFinal)
                                                                             && x.Destination == _taxiPoints.First()))
                            {
                                activity?.AddTag("Response", "Hold Short");
                                await SendMessage($"Hold short {_currentTaxiPoint.Name}");
                            }
                            else
                            {
                                activity?.AddTag("Response", "RunwayCrossing");
                                // If we have reached this bit in the code then the current taxi point is a runway that is not the terminus of the route
                                // so tell the player they are good to cross.
                                await SendMessage($"cross {_currentTaxiPoint.Name} at your discretion");
                            }
                        }
                    }
                } else if (_taxiPoints.Count == 1)
                {
                    Logger.Error(
                        $"{_currentTaxiPoint.Name} is the last point in the taxi path but is not a runway");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking taxi progress");
            }

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
                Logger.Info($"Outgoing Transmission: {response}");
                _responseQueue.Enqueue(audioData);
            }
        }
    }
}

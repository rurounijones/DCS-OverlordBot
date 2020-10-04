using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using RurouniJones.DCS.Airfields.Structure;
using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.SpeechOutput;

namespace RurouniJones.DCS.OverlordBot.Controllers
{
    class TaxiProgressChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static ConcurrentDictionary<string, TaxiProgressChecker> TaxiChecks = new ConcurrentDictionary<string, TaxiProgressChecker>();

        private readonly Timer _checkTimer;
        private readonly Player _sender;

        private readonly string _airfieldName;
        private readonly string _voice;

        private readonly List<TaxiPoint> _taxiPoints;
        private TaxiPoint _currentTaxiPoint;

        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private const double CheckInterval = 1000; // milliseconds

        public TaxiProgressChecker(Player sender, string airfieldName, string voice, List<TaxiPoint> taxiPoints, ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _airfieldName = airfieldName;
            _voice = voice;
            _taxiPoints = taxiPoints;
            _responseQueue = responseQueue;

            if (TaxiChecks.ContainsKey(_sender.Id))
            {
                TaxiChecks[_sender.Id].Stop();
                TaxiChecks.TryRemove(_sender.Id, out _);
            }

            // Do once immediately so we get the current taxi-point
            Task.Run(async() => await CheckAsync());

            _checkTimer = new Timer(CheckInterval);
            _checkTimer.Elapsed += async (s, e) => await CheckAsync();

            _checkTimer.Start();

            TaxiChecks[_sender.Id] = this;
        }

        private void Stop()
        {
            Logger.Debug($"Stopping taxi progress check for {_sender.Id}");
            _checkTimer.Stop();
            _checkTimer.Close();
            TaxiChecks.TryRemove(_sender.Id, out _);
        }

        private async Task CheckAsync()
        {
            try
            {
                Logger.Debug($"Peforming Taxi Progress check for {_sender.Id}");
                var previousId = _sender.Id;
                await GameQuerier.PopulatePilotData(_sender);

                // If the caller does not exist any more or the ID has been reused for a different object then cancel the check.
                if (_sender.Id == null || _sender.Id != previousId)
                {
                    _sender.Id = "DELETED";
                    Logger.Debug($"Stopping Taxi Progress Check. CallerId changed, New: {_sender.Id} , Old: {previousId}.");
                    Stop();
                    return;
                }

                var closestPoint =_taxiPoints.OrderBy(taxiPoint => taxiPoint.DistanceTo(_sender.Position.Coordinate)).First();

                // If this is true then we don't need to say the same commands again etc.
                if (_currentTaxiPoint == closestPoint)
                    return;

                // We want to get rid of all the previous TaxiPoints in the list. We do this instead of just getting rid of the first in case
                // somehow the pilot manage to skip a TaxiPoint by going fast enough that they passed it before the check.
                var index =_taxiPoints.IndexOf(_currentTaxiPoint);

                if (index > 1)
                    Logger.Trace($" {_sender.Id} skipped at least one taxi point");
                
                for (var i = _taxiPoints.Count - 1; i >= 0; i--)
                {
                    if (i > index) continue;
                    Logger.Trace($"Removing {_taxiPoints[i].Name} from route of {_sender.Id}");
                    _taxiPoints.RemoveAt(i);
                }

                // Player has reached the end of the taxi route.
                // We will probably need to change this when we actually have "Hold short" and "Line up and wait" commands.
                if (_taxiPoints.Count <= 1)
                {
                    Logger.Debug($"Stopping Taxi Progress Check. {_sender.Id} has reached the end of the taxi route at {_currentTaxiPoint.Name}");
                    Stop();
                    return;
                }

                _currentTaxiPoint = closestPoint;
                Logger.Debug($"New closest TaxiPoint to {_sender.Id} is {_currentTaxiPoint.Name}");


                Logger.Debug($"Next TaxiPoint for {_sender.Id} is {_taxiPoints[1].Name}");
                if (!(_currentTaxiPoint is Runway))
                    return;

                // If we have reached this bit in the code then the next taxi point is a runway so shout at the player.
                var response = $"{_sender.Callsign}, {_airfieldName} ground, cross {_currentTaxiPoint.Name} at your discretion";

                var ssmlResponse = $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>";

                var audioData = await Speaker.CreateResponse(ssmlResponse);

                if (audioData != null)
                {
                    Logger.Info($"Outgoing Transmission: {response}");
                    _responseQueue.Enqueue(audioData);
                }
            } catch (Exception ex)
            {
                Logger.Error(ex, "Error checking taxi progress");
            }

        }
    }
}

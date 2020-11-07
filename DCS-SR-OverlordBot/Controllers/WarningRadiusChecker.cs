using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using NLog;
using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.Intents;
using RurouniJones.DCS.OverlordBot.SpeechOutput;

namespace RurouniJones.DCS.OverlordBot.Controllers
{
    internal class WarningRadiusChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static readonly ConcurrentDictionary<string, List<string>> WarningStates = new ConcurrentDictionary<string, List<string>>();
        public static readonly ConcurrentDictionary<string, WarningRadiusChecker> WarningChecks = new ConcurrentDictionary<string, WarningRadiusChecker>();

        private readonly Timer _checkTimer;
        private readonly Player _sender;
        private readonly string _awacs;
        private readonly string _voice;
        private readonly int _distance;
        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private const double CheckInterval = 5000; // milliseconds

        private string _previousId;

        public WarningRadiusChecker(Player sender, string awacs, string voice, int distance, ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _awacs = awacs;
            _voice = voice;
            _distance = distance;
            _responseQueue = responseQueue;

            if (WarningChecks.ContainsKey(_sender.Id))
            {
                Logger.Trace($"{_sender.Id} - {_sender.Callsign} already in WarningChecks");
                WarningChecks[_sender.Id].Stop();
                Logger.Trace($"{_sender.Id} - {_sender.Callsign} removed from WarningChecks: {WarningChecks.TryRemove(_sender.Id, out _)}");
            }

            if (WarningStates.ContainsKey(_sender.Id))
            {
                Logger.Trace($"{_sender.Id} - {_sender.Callsign} already in WarningStates");
                WarningStates.TryRemove(_sender.Id, out _);
                Logger.Trace($"{_sender.Id} - {_sender.Callsign} removed from WarningStates: {WarningChecks.TryRemove(_sender.Id, out _)}");

            }

            _checkTimer = new Timer(CheckInterval);
            _checkTimer.Elapsed += async (s, e) => await CheckAsync();

            Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Starting {distance} mile Radius Warning Check");

            _checkTimer.Start();

            WarningChecks[_sender.Id] = this;
        }

        public void Stop()
        {
            var id = _sender.Id.Equals("DELETED") ? _previousId : _sender.Id;
            Logger.Debug($"{id} - {_sender.Callsign}: Stopping Warning Check");
            _checkTimer.Stop();
            _checkTimer.Dispose();
            Logger.Trace($"{id} - {_sender.Callsign} removed from WarningChecks: {WarningChecks.TryRemove(id, out _)}");
            Logger.Trace($"{id} - {_sender.Callsign} removed from WarningStates: {WarningStates.TryRemove(id, out _)}");
        }

        private async Task CheckAsync()
        {
            try
            {
                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Peforming Warning Radius check");

                if (WarningStates.ContainsKey(_sender.Id) == false)
                {
                    WarningStates.TryAdd(_sender.Id, new List<string>());
                }

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

                var contact =
                    await GameQuerier.GetBogeyDope(_sender.Coalition, _sender.Group, _sender.Flight, _sender.Plane);

                if (contact == null)
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: No contacts found");
                    return;
                }

                if (contact.Range > _distance)
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Contact {contact.Id} is more than {_distance} miles ({contact.Range})");
                    return;
                }

                if (WarningStates[_sender.Id].Contains(contact.Id))
                {
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Contact {contact.Id} already reported");
                    return;
                }

                Logger.Debug($"{_sender.Id} - {_sender.Callsign}: New contact {contact.Id} at {contact.Range} miles");

                using (var activity =
                    Constants.ActivitySource.StartActivity("WarningRadiusChecker.SendWarning", ActivityKind.Consumer))
                {
                    var response = $"{_sender.Callsign}, {_awacs}, Threat, {BogeyDope.BuildResponse(_sender, contact)}";
                    Logger.Debug($"{_sender.Id} - {_sender.Callsign}: Response: {response}");

                    var ssmlResponse =
                        $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>";

                    var audioData = await Speaker.CreateResponse(ssmlResponse);

                    if (audioData != null)
                    {
                        Logger.Info($"{_sender.Id} - {_sender.Callsign}: Outgoing Transmission: {response}");
                        _responseQueue.Enqueue(audioData);
                        WarningStates[_sender.Id].Add(contact.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{_sender.Id} - {_sender.Callsign}: Error checking warning radius");
            }
        }
    }
}

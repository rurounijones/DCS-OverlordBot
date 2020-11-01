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

        private static readonly ConcurrentDictionary<string, List<string>> WarningStates = new ConcurrentDictionary<string, List<string>>();
        public static ConcurrentDictionary<string, WarningRadiusChecker> WarningChecks = new ConcurrentDictionary<string, WarningRadiusChecker>();

        private readonly Timer _checkTimer;
        private readonly Player _sender;
        private readonly string _awacs;
        private readonly string _voice;
        private readonly int _distance;
        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private const double CheckInterval = 5000; // milliseconds

        public WarningRadiusChecker(Player sender, string awacs, string voice, int distance, ConcurrentQueue<byte[]> responseQueue)
        {
            _sender = sender;
            _awacs = awacs;
            _voice = voice;
            _distance = distance;
            _responseQueue = responseQueue;

            if (WarningChecks.ContainsKey(_sender.Id))
            {
                WarningChecks[_sender.Id].Stop();
                WarningChecks.TryRemove(_sender.Id, out _);
            }

            _checkTimer = new Timer(CheckInterval);
            _checkTimer.Elapsed += async (s, e) => await CheckAsync();

            Logger.Debug($"Starting {distance} mile Radius Warning Check for {sender.Id}");

            _checkTimer.Start();

            WarningChecks[_sender.Id] = this;
        }

        private void Stop()
        {
            Logger.Debug($"Stopping Warning Check for {_sender.Id}");
            _checkTimer.Stop();
            WarningStates.TryRemove(_sender.Id, out _);
            _checkTimer.Close();
        }

        private async Task CheckAsync()
        {
            try
            {
                Logger.Debug($"Peforming Warning Radius check for {_sender.Id}");

                if (WarningStates.ContainsKey(_sender.Id) == false)
                {
                    WarningStates.TryAdd(_sender.Id, new List<string>());
                }

                var previousId = _sender.Id;
                await GameQuerier.PopulatePilotData(_sender);

                // If the caller does not exist any more or the ID has been reused for a different object
                // then cancel the check.
                if (_sender.Id == null || _sender.Id != previousId)
                {
                    _sender.Id = "DELETED";
                    Logger.Debug(
                        $"Stopping Warning Radius Check. CallerId changed, New: {_sender.Id} , Old: {previousId}.");
                    Stop();
                    return;
                }

                var contact =
                    await GameQuerier.GetBogeyDope(_sender.Coalition, _sender.Group, _sender.Flight, _sender.Plane);

                if (contact == null)
                {
                    Logger.Debug($"No contacts found for {_sender.Id})");
                    return;
                }

                if (contact.Range > _distance)
                {
                    Logger.Debug($"Contact {contact.Id} is more than {_distance} miles ({contact.Range})");
                    return;
                }

                if (WarningStates[_sender.Id].Contains(contact.Id))
                {
                    Logger.Debug($"Contact {contact.Id} already reported");
                    return;
                }

                Logger.Debug($"New contact {contact.Id}");

                using (var activity =
                    Constants.ActivitySource.StartActivity("WarningRadiusChecker.SendWarning", ActivityKind.Consumer))
                {
                    var response = $"{_sender.Callsign}, {_awacs}, Threat, {BogeyDope.BuildResponse(_sender, contact)}";
                    Logger.Debug($"Response: {response}");

                    var ssmlResponse =
                        $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>";

                    var audioData = await Speaker.CreateResponse(ssmlResponse);

                    if (audioData != null)
                    {
                        Logger.Info($"Outgoing Transmission: {response}");
                        _responseQueue.Enqueue(audioData);
                        WarningStates[_sender.Id].Add(contact.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking warning radius");
            }
            finally
            {
                Stop();
            }
        }
    }
}

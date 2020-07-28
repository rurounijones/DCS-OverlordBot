using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers
{
    class WarningRadiusChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static ConcurrentDictionary<string, List<string>> _warningStates = new ConcurrentDictionary<string, List<string>>();
        public static ConcurrentDictionary<string, WarningRadiusChecker> WarningChecks = new ConcurrentDictionary<string, WarningRadiusChecker>();

        private readonly Timer _checkTimer;
        private readonly Player _sender;
        private readonly string _awacs;
        private readonly string _voice;
        private readonly int _distance;
        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private static readonly double CHECK_INTERVAL = 5000; // milliseconds

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

            _checkTimer = new Timer(CHECK_INTERVAL);
            _checkTimer.Elapsed += async (s, e) => await CheckAsync();

            Logger.Debug($"Starting {distance} mile Radius Warning Check for {sender.Id}");

            _checkTimer.Start();

            WarningChecks[_sender.Id] = this;
        }

        private void Stop()
        {
            Logger.Debug($"Stopping Warning Check for {_sender.Id}");
            _checkTimer.Stop();
            _warningStates.TryRemove(_sender.Id, out _);
            _checkTimer.Close();
        }

        private async Task CheckAsync()
        {
            try
            {
                Logger.Debug($"Peforming Warning Radius check for {_sender.Id}");

                if (_warningStates.ContainsKey(_sender.Id) == false)
                {
                    _warningStates.TryAdd(_sender.Id, new List<string>());
                }
                var previousId = _sender.Id;
                await GameQuerier.GetPilotData(_sender);

                // If the caller does not exist any more or the ID has been reused for a different object
                // then cancel the check.
                if (_sender.Id == null || _sender.Id != previousId)
                {
                    _sender.Id = "DELETED";
                    Logger.Debug($"Stopping Warning Radius Check. CallerId changed, New: {_sender.Id} , Old: {previousId}.");
                    Stop();
                    return;
                }

                Contact contact = await GameQuerier.GetBogeyDope(_sender.Coalition, _sender.Group, _sender.Flight, _sender.Plane);

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

                if (_warningStates[_sender.Id].Contains(contact.Id))
                {
                    Logger.Debug($"Contact {contact.Id} already reported");
                    return;
                }

                Logger.Debug($"New contact {contact.Id}");

                var response = $"{_sender.Callsign}, {_awacs}, Threat, {BogeyDope.BuildResponse(_sender, contact)}";
                Logger.Debug($"Response: {response}");

                var ssmlResponse = $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>";

                byte[] audioData = await Speaker.CreateResponse(ssmlResponse);

                if (audioData != null)
                {
                    Logger.Info($"Outgoing Transmission: {response}");
                    _responseQueue.Enqueue(audioData);
                    _warningStates[_sender.Id].Add(contact.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking warning radius");
            }
        }
    }
}

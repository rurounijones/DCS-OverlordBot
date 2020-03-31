using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using NLog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    class WarningRadiusChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static ConcurrentDictionary<string, List<string>> _warningStates = new ConcurrentDictionary<string, List<string>>();
        public static ConcurrentDictionary<string, WarningRadiusChecker> WarningChecks = new ConcurrentDictionary<string, WarningRadiusChecker>();

        private readonly Timer _checkTimer;
        private readonly string _callerId;
        private readonly Sender _sender;
        private readonly string _awacs;
        private readonly string _voice;
        private readonly int _distance;
        private readonly ConcurrentQueue<byte[]> _responseQueue;

        private static readonly double CHECK_INTERVAL = 5000; // milliseconds

        public WarningRadiusChecker(string callerId, Sender sender, string awacs, string voice, int distance, ConcurrentQueue<byte[]> responseQueue) 
        {
            _callerId = callerId;
            _sender = sender;
            _awacs = awacs;
            _voice = voice;
            _distance = distance;
            _responseQueue = responseQueue;

            if(WarningChecks.ContainsKey(_callerId))
            {
                WarningChecks[_callerId].Stop();
                WarningChecks.TryRemove(_callerId, out _);
            }

            _checkTimer = new Timer(CHECK_INTERVAL);
            _checkTimer.Elapsed += async (s, e) => await CheckAsync();

            Logger.Info($"Starting {distance} mile Radius Warning Check for {callerId}");

            _checkTimer.Start();

            WarningChecks[_callerId] = this;
        }

        private void Stop()
        {
            Logger.Info($"Stopping Warning Check for {_callerId}");
            _checkTimer.Stop();
            _warningStates.TryRemove(_callerId, out _);
            _checkTimer.Close();
        }

        private async Task CheckAsync()
        {
            Logger.Info($"Peforming Warning Radius check for {_callerId}");

            if(_warningStates.ContainsKey(_callerId) == false)
            {
                _warningStates.TryAdd(_callerId, new List<string>());
            }

            string callerCheckId = await GameState.DoesPilotExist(_sender.Group, _sender.Flight, _sender.Plane);

            // If the caller does not exist any more or the ID has been reused for a different object
            // then cancel the check.
            if(callerCheckId != _callerId)
            {
                Logger.Debug($"CallerId changed, New: {callerCheckId} , Old: {_callerId}");
                Stop();
                return;
            }

            Contact contact = await GameState.GetBogeyDope(_sender.Group, _sender.Flight, _sender.Plane);

            if (contact.Range > _distance)
            {
                Logger.Debug($"Contact {contact.Id} is more than {_distance} miles ({contact.Range})");
                return;
            }

            if (_warningStates[_callerId].Contains(contact.Id))
            {
                Logger.Debug($"Contact {contact.Id} already reported");
                return;
            }

            Logger.Debug($"New contact {contact.Id}");

            _warningStates[_callerId].Add(contact.Id);

            var response = $"{_sender}, {_awacs}, Threat, {BogeyDope.BuildResponse(contact)}";
            Logger.Debug($"Response: {response}");

            response = $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>";

            byte[] audioData = await Speaker.CreateResponse(response);

            if (audioData != null) {
                _responseQueue.Enqueue(audioData);
            }
        }
    }
}

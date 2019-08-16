using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    class SpeechRecognitionListener
    {
        // Creates an instance of a speech config with specified subscription key and service region.
        // Replace with your own subscription key and service region (e.g., "westus").
        private static readonly SpeechConfig _speechConfig = SpeechConfig.FromSubscription("YourSubscriptionKey", "YourRegion");

        private readonly BufferedWaveProviderStreamReader _streamReader;
        private readonly SpeechRecognizer _recognizer;
        private readonly AudioConfig _audioConfig;

        public SpeechRecognitionListener(BufferedWaveProvider bufferedWaveProvider)
        {
            // If you are using Custom Speech endpoint
            _speechConfig.EndpointId = "YourEndPointId";

            _streamReader = new BufferedWaveProviderStreamReader(bufferedWaveProvider);
            _audioConfig = AudioConfig.FromStreamInput(_streamReader, AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
            _recognizer = new SpeechRecognizer(_speechConfig, _audioConfig);
        }

        public async Task StartListeningAsync()
        {
            var stopRecognition = new TaskCompletionSource<int>();

            // Subscribes to events.
            _recognizer.Recognizing += (s, e) =>
            {
                Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
            };

            _recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"RECOGNIZED: {e.Result.Text}");
                    string data = Task.Run(() => LuisService.ParseIntent(e.Result.Text)).Result;
                    Console.WriteLine($"INTENT: {data}");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                }
            };

            _recognizer.Canceled += (s, e) =>
            {
                Console.WriteLine($"CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                    Console.WriteLine($"CANCELED: Did you update the subscription info?");
                }

                stopRecognition.TrySetResult(0);
            };

            _recognizer.SessionStarted += (s, e) =>
            {
                Console.WriteLine("\nSession started event.");
            };

            _recognizer.SessionStopped += (s, e) =>
            {
                Console.WriteLine("\nSession stopped event.");
                Console.WriteLine("\nStop recognition.");
                stopRecognition.TrySetResult(0);
            };

            // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
            await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            // Waits for completion.
            // Use Task.WaitAny to keep the task rooted.
            Task.WaitAny(new[] { stopRecognition.Task });

            // Stops recognition.
            await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }
    }
}

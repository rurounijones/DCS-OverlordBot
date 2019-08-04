using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Intent;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    public class RadioListener
    {

        IntentRecognizer _recognizer;
        Speaker _speaker;

        public RadioListener(IntentRecognizer recognizer, BufferedWaveProvider responseBuffer)
        {
            _recognizer = recognizer;

            var model = LanguageUnderstandingModel.FromAppId("2414f770-d586-4707-8e0b-93ce0738c5bf");
            _recognizer.AddIntent(model, "RequestBogeyDope", "requestBogeyDope");

            _speaker = new Speaker(responseBuffer);
        }

        public async Task StartListeningAsync()
        {
            // Since transmissions over STS are PTT we shouldn't really need keyword listening.
            // But keep this here just in case.

            // Creates an instance of a keyword recognition model. Update this to
            // point to the location of your keyword recognition model.
            //var keywordModel = KeywordRecognitionModel.FromFile("Overlord/overlord.table");
            // The phrase your keyword recognition model triggers on.
            //var keyword = "overlord";

            Console.WriteLine($"Started listening Async");

            // The TaskCompletionSource to stop recognition.
            var stopRecognition = new TaskCompletionSource<int>();

            // Subscribes to events.
            _recognizer.Recognizing += (s, e) => {
                Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
            };

            _recognizer.Recognized += async (s, e) => {
                if (e.Result.Reason == ResultReason.RecognizedIntent)
                {
                    Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                    Console.WriteLine($"Intent Id: {e.Result.IntentId}.");
                    Console.WriteLine($"Language Understanding JSON: {e.Result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult)}.");

                    string luisData = e.Result.Properties.GetProperty(PropertyId.LanguageUnderstandingServiceResponse_JsonResult);

                    switch (e.Result.IntentId)
                    {
                        case "requestBogeyDope":
                            var response = await RequestBogeyDope.Process(luisData);
                            await _speaker.SendResponse(response);
                            break;
                    }
                }
                else if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                    Console.WriteLine($"Intent not recognized.");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                }
            };

            _recognizer.Canceled += (s, e) => {
                Console.WriteLine($"CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                    Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                    Console.WriteLine($"CANCELED: Did you update the subscription info?");
                }

                //stopRecognition.TrySetResult(0);
            };

            _recognizer.SessionStarted += (s, e) => {
                Console.WriteLine("Session started event.");
            };

            _recognizer.SessionStopped += (s, e) => {
                Console.WriteLine("Session stopped event.");
                Console.WriteLine("Stop recognition.");
                //stopRecognition.TrySetResult(0);
            };

            _recognizer.SpeechStartDetected += (s, e) => {
                Console.WriteLine("Speech Start Detected event.");
            };

            _recognizer.SpeechEndDetected += (s, e) => {
                Console.WriteLine("Speech End Detected event.");
            };

            // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
            Console.WriteLine("Starting continuous recognition");
            await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
            //Console.WriteLine("Starting Keyword recognition");
            //await _recognizer.StartKeywordRecognitionAsync(keywordModel).ConfigureAwait(false);

            // Waits for completion.
            // Use Task.WaitAny to keep the task rooted.
            Task.WaitAny(new[] { stopRecognition.Task });

            // Stops recognition. Change this if using Keyword
            await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }
    }
}

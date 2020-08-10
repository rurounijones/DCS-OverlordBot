using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Discord;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using FragLabs.Audio.Codecs;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using NLog;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    public class SpeechRecognitionListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Used when an exception is thrown so that the caller isn't left wondering.
        private static readonly byte[] _failureMessage = File.ReadAllBytes("Overlord/equipment-failure.wav");

        // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private static TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9);

        private readonly BufferedWaveProviderStreamReader _streamReader;
        private readonly AudioConfig _audioConfig;
        private readonly OpusEncoder _encoder;

        public readonly AbstractController controller;

        public UdpVoiceHandler _voiceHandler;

        public bool TimedOut;

#pragma warning disable IDE0051 // Allows OverlordBot to listen for a specific word to start listening. Currently not used although the setup has all been done.
        // This is due to wierd state transition errors that I cannot be bothered to debug. Possible benefit is less calls to Speech endpoint but
        // not sure if that is good enough or not to keep investigating.
        private readonly KeywordRecognitionModel _wakeWord;
#pragma warning restore IDE0051

        public SpeechRecognitionListener(BufferedWaveProvider bufferedWaveProvider, ConcurrentQueue<byte[]> responseQueue, RadioInformation radioInfo)
        {
            radioInfo.TransmissionQueue = responseQueue;

            switch (radioInfo.botType)
            {
                case "ATC":
                    controller = new AtcController()
                    {
                        Callsign = radioInfo.name,
                        Voice = radioInfo.voice,
                        Radio = radioInfo
                    };
                    break;
                case "AWACS":
                    controller = new AwacsController()
                    {
                        Callsign = radioInfo.name,
                        Voice = radioInfo.voice,
                        Radio = radioInfo
                    };
                    break;
                default:
                    controller = new MuteController()
                    {
                        Callsign = radioInfo.name,
                        Voice = null,
                        Radio = null
                    };
                    break;
            }

            _encoder = OpusEncoder.Create(AudioManager.INPUT_SAMPLE_RATE, 1, FragLabs.Audio.Codecs.Opus.Application.Voip);
            _encoder.ForwardErrorCorrection = false;
            _encoder.FrameByteCount(AudioManager.SEGMENT_FRAMES);

            _streamReader = new BufferedWaveProviderStreamReader(bufferedWaveProvider);
            _audioConfig = AudioConfig.FromStreamInput(_streamReader, AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));

            //_wakeWord = KeywordRecognitionModel.FromFile($"Overlord/WakeWords/{callsign}.table");
        }

        // Gets an authorization token by sending a POST request to the token service.
        public static async Task<string> GetToken()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Properties.Settings.Default.SpeechSubscriptionKey);
                UriBuilder uriBuilder = new UriBuilder("https://" + Properties.Settings.Default.SpeechRegion + ".api.cognitive.microsoft.com/sts/v1.0/issueToken");

                using (var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null))
                {
                    if (result.IsSuccessStatusCode)
                    {
                        return await result.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        throw new HttpRequestException($"Cannot get token from {uriBuilder}. Error: {result.StatusCode}");
                    }
                }
            }
        }

        // Renews authorization token periodically until cancellationToken is cancelled.
        public static Task StartTokenRenewTask(CancellationToken cancellationToken, SpeechRecognizer recognizer)
        {
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(RefreshTokenDuration, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        recognizer.AuthorizationToken = await GetToken();
                    }
                }
            });
        }

        public async Task StartListeningAsync()
        {
            Logger.Debug($"Started Continuous Recognition");

            // Initialize the recognizer
            var authorizationToken = Task.Run(() => GetToken()).Result;
            SpeechConfig speechConfig = SpeechConfig.FromAuthorizationToken(authorizationToken, Properties.Settings.Default.SpeechRegion);
            speechConfig.EndpointId = Properties.Settings.Default.SpeechCustomEndpointId;
            SpeechRecognizer recognizer = new SpeechRecognizer(speechConfig, _audioConfig);

            // Setup the cancellation code
            var stopRecognition = new TaskCompletionSource<int>();
            CancellationTokenSource source = new CancellationTokenSource();

            // Start the token renewal so we can do long-running recognition.
            var tokenRenewTask = StartTokenRenewTask(source.Token, recognizer);

            recognizer.Recognized += async (s, e) =>
            {
                await ProcessRadioCall(e);
            };

            recognizer.Canceled += async (s, e) =>
            {
                Logger.Trace($"CANCELLED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Logger.Trace($"CANCELLED: ErrorCode={e.ErrorCode}");
                    Logger.Trace($"CANCELLED: ErrorDetails={e.ErrorDetails}");

                    if (e.ErrorCode != CancellationErrorCode.BadRequest && e.ErrorCode != CancellationErrorCode.ConnectionFailure)
                    {
                        controller.Radio.TransmissionQueue.Enqueue(_failureMessage);
                    }
                }
                stopRecognition.TrySetResult(1);
            };

            recognizer.SpeechStartDetected += (s, e) =>
            {
                Logger.Trace("\nSpeech started event.");
            };

            recognizer.SpeechEndDetected += (s, e) =>
            {
                Logger.Trace("\nSpeech ended event.");
            };

            recognizer.SessionStarted += (s, e) =>
            {
                Logger.Trace("\nSession started event.");
            };

            recognizer.SessionStopped += (s, e) =>
            {
                Logger.Trace("\nSession stopped event.");
                stopRecognition.TrySetResult(0);
            };

            // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            // Waits for completion.
            // Use Task.WaitAny to keep the task rooted.
            Task.WaitAny(new[] { stopRecognition.Task });

            // Stops recognition.
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            source.Cancel();
            Logger.Debug($"Stopped Continuous Recognition");
            TimedOut = true;
        }

        private async Task ProcessRadioCall(SpeechRecognitionEventArgs e)
        {
            string response = null;

            try
            {
                switch (e.Result.Reason)
                {
                    case ResultReason.RecognizedSpeech:
                        Logger.Info($"Incoming Transmission: {e.Result.Text}");
                        string luisJson = Task.Run(() => LuisService.ParseIntent(e.Result.Text)).Result;
                        Logger.Debug($"LUIS Response: {luisJson}");

                        var radioCall = new BaseRadioCall(luisJson);

                        response = controller.ProcessRadioCall(radioCall);

                        Logger.Info($"Outgoing Transmission: {response}");

                        if (controller.Radio.discordTransmissionLogChannelId > 0)
                            LogTransmissionToDiscord(radioCall, response);
                        break;
                    case ResultReason.NoMatch:
                        Logger.Debug($"NOMATCH: Speech could not be recognized.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing radio call");
                controller.Radio.TransmissionQueue.Enqueue(_failureMessage);
                response = null;
            }
            if (!string.IsNullOrEmpty(response))
            {
                var audioResponse = await Task.Run(() => Speaker.CreateResponse($"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{controller.Voice}\">{response}</voice></speak>"));
                if (audioResponse != null)
                {
                    controller.Radio.TransmissionQueue.Enqueue(audioResponse);
                }
                else
                {
                    controller.Radio.TransmissionQueue.Enqueue(_failureMessage);
                }
            }
        }

        private void LogTransmissionToDiscord(BaseRadioCall radioCall, string response)
        {
            string transmission = $"Transmission Intent: {radioCall.Intent}\n" +
                $"Request: {radioCall.Message}\n" +
                $"Response: {response ?? "INGORED"}";
            _ = DiscordClient.LogTransmissionToDiscord(transmission, controller.Radio).ConfigureAwait(false);
        }

        public async Task SendTransmission(string message)
        {
            var audioResponse = await Task.Run(() => Speaker.CreateResponse($"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{controller.Voice}\">{message}</voice></speak>"));
            controller.Radio.TransmissionQueue.Enqueue(audioResponse);
        }
    }
}

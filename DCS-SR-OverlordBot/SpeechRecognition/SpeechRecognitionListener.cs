using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using FragLabs.Audio.Codecs;
using FragLabs.Audio.Codecs.Opus;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using NLog;
using RurouniJones.DCS.OverlordBot.Audio.Managers;
using RurouniJones.DCS.OverlordBot.Controllers;
using RurouniJones.DCS.OverlordBot.Discord;
using RurouniJones.DCS.OverlordBot.Network;
using RurouniJones.DCS.OverlordBot.RadioCalls;
using RurouniJones.DCS.OverlordBot.SpeechOutput;

namespace RurouniJones.DCS.OverlordBot.SpeechRecognition
{
    public class SpeechRecognitionListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _logClientId;

        // Used when an exception is thrown so that the caller isn't left wondering.
        private static readonly byte[] FailureMessage = File.ReadAllBytes("Data/equipment-failure.wav");

        // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private static readonly TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9);

        private readonly AudioConfig _audioConfig;

        public readonly AbstractController Controller;

        public SrsAudioClient VoiceHandler;

        public bool TimedOut;

        // Allows OverlordBot to listen for a specific word to start listening. Currently not used although the setup has all been done.
        // This is due to wierd state transition errors that I cannot be bothered to debug. Possible benefit is less calls to Speech endpoint but
        // not sure if that is good enough or not to keep investigating.
        //private readonly KeywordRecognitionModel _wakeWord;

        public SpeechRecognitionListener(BufferedWaveProvider bufferedWaveProvider, ConcurrentQueue<byte[]> responseQueue, RadioInformation radioInfo)
        {
            radioInfo.TransmissionQueue = responseQueue;
            _logClientId = radioInfo.name;

            switch (radioInfo.botType)
            {
                case "ATC":
                    Controller = new AtcController
                    {
                        Callsign = radioInfo.callsign,
                        Voice = radioInfo.voice,
                        Radio = radioInfo
                    };
                    break;
                case "AWACS":
                    Controller = new AwacsController
                    {
                        Callsign = radioInfo.callsign,
                        Voice = radioInfo.voice,
                        Radio = radioInfo
                    };
                    break;
                default:
                    Controller = new MuteController
                    {
                        Callsign = radioInfo.callsign,
                        Voice = null,
                        Radio = null
                    };
                    break;
            }

            var encoder = OpusEncoder.Create(AudioManager.InputSampleRate, 1, Application.Voip);
            encoder.ForwardErrorCorrection = false;
            encoder.FrameByteCount(AudioManager.SegmentFrames);

            var streamReader = new BufferedWaveProviderStreamReader(bufferedWaveProvider);
            _audioConfig = AudioConfig.FromStreamInput(streamReader, AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));

            //_wakeWord = KeywordRecognitionModel.FromFile($"Overlord/WakeWords/{callsign}.table");
        }

        // Gets an authorization token by sending a POST request to the token service.
        public static async Task<string> GetToken()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Properties.Settings.Default.SpeechSubscriptionKey);
                var uriBuilder = new UriBuilder("https://" + Properties.Settings.Default.SpeechRegion + ".api.cognitive.microsoft.com/sts/v1.0/issueToken");

                using (var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null))
                {
                    if (result.IsSuccessStatusCode)
                    {
                        return await result.Content.ReadAsStringAsync();
                    }
                    throw new HttpRequestException($"Cannot get token from {uriBuilder}. Error: {result.StatusCode}");
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
            }, cancellationToken);
        }

        public async Task StartListeningAsync()
        {
            Logger.Debug($"{_logClientId}| Started Continuous Recognition");

            // Initialize the recognizer
            var authorizationToken = Task.Run(GetToken).Result;
            var speechConfig = SpeechConfig.FromAuthorizationToken(authorizationToken, Properties.Settings.Default.SpeechRegion);
            speechConfig.EndpointId = Properties.Settings.Default.SpeechCustomEndpointId;
            var recognizer = new SpeechRecognizer(speechConfig, _audioConfig);

            // Setup the cancellation code
            var stopRecognition = new TaskCompletionSource<int>();
            var source = new CancellationTokenSource();

            // Start the token renewal so we can do long-running recognition.
            var tokenRenewTask = StartTokenRenewTask(source.Token, recognizer);

            recognizer.Recognized += async (s, e) =>
            {
                await ProcessRadioCall(e);
            };

            recognizer.Canceled += async (s, e) =>
            {
                Logger.Trace($"{_logClientId}| CANCELLED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    Logger.Trace($"{_logClientId}| CANCELLED: ErrorCode={e.ErrorCode}");
                    Logger.Trace($"{_logClientId}| CANCELLED: ErrorDetails={e.ErrorDetails}");

                    if (e.ErrorCode != CancellationErrorCode.BadRequest && e.ErrorCode != CancellationErrorCode.ConnectionFailure)
                    {
                        Controller.Radio.TransmissionQueue.Enqueue(FailureMessage);
                    }
                }
                stopRecognition.TrySetResult(1);
            };

            recognizer.SpeechStartDetected += (s, e) =>
            {
                Logger.Trace($"{_logClientId}| Speech started event.");
            };

            recognizer.SpeechEndDetected += (s, e) =>
            {
                Logger.Trace($"{_logClientId}| Speech ended event.");
            };

            recognizer.SessionStarted += (s, e) =>
            {
                Logger.Trace($"{_logClientId}| Session started event.");
            };

            recognizer.SessionStopped += (s, e) =>
            {
                Logger.Trace($"{_logClientId}| Session stopped event.");
                stopRecognition.TrySetResult(0);
            };

            // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            // Waits for completion.
            // Use Task.WaitAny to keep the task rooted.
            Task.WaitAny(stopRecognition.Task);

            // Stops recognition.
            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            source.Cancel();
            Logger.Debug($"{_logClientId}| Stopped Continuous Recognition");
            TimedOut = true;
        }

        private async Task ProcessRadioCall(SpeechRecognitionEventArgs e)
        {
            try
            {
                switch (e.Result.Reason)
                {
                    case ResultReason.RecognizedSpeech:
                        await ProcessRecognizedCall(e);
                        break;
                    case ResultReason.NoMatch:
                        Logger.Debug($"{_logClientId}| NOMATCH: Speech could not be recognized.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{_logClientId}| Error processing radio call");
                Controller.Radio.TransmissionQueue.Enqueue(FailureMessage);
            }
        }

        private async Task ProcessRecognizedCall(SpeechRecognitionEventArgs e)
        {
            Logger.Info($"{_logClientId}| Incoming Transmission: {e.Result.Text}");
            var luisJson = Task.Run(() => LuisService.ParseIntent(e.Result.Text)).Result;
            Logger.Debug($"{_logClientId}| LUIS Response: {luisJson}");

            var radioCall = new BaseRadioCall(luisJson);

            var response = Controller.ProcessRadioCall(radioCall);

            if (!string.IsNullOrEmpty(response))
            {
                Logger.Info($"{_logClientId}| Outgoing Transmission: {response}");
                var audioResponse = await Task.Run(() => Speaker.CreateResponse(
                    $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{Controller.Voice}\">{response}</voice></speak>"));
                Controller.Radio.TransmissionQueue.Enqueue(audioResponse ?? FailureMessage);
            }
            else
            {
                Logger.Info($"{_logClientId}| Radio Call Ignored due to null response for Radio Call Processing");
            }

            if (Controller.Radio.discordTransmissionLogChannelId > 0)
                LogTransmissionToDiscord(radioCall, response);
        }

        private void LogTransmissionToDiscord(IRadioCall radioCall, string response)
        {
            Logger.Debug($"{_logClientId}| Building Discord Request/Response message");
            var transmission = $"Transmission Intent: {radioCall.Intent}\n" +
                               $"Request: {radioCall.Message}\n" +
                               $"Response: {response ?? "**IGNORED**"}";
            _ = DiscordClient.LogTransmissionToDiscord(transmission, Controller.Radio).ConfigureAwait(false);
        }

        public async Task SendTransmission(string message)
        {
            var audioResponse = await Task.Run(() => Speaker.CreateResponse($"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{Controller.Voice}\">{message}</voice></speak>"));
            Controller.Radio.TransmissionQueue.Enqueue(audioResponse);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
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
using RurouniJones.DCS.OverlordBot.Util;

namespace RurouniJones.DCS.OverlordBot.SpeechRecognition
{
    public class SpeechRecognitionListener
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _logClientId;

        // Used when an exception is thrown so that the caller isn't left wondering.
        private static readonly byte[] FailureMessage = File.ReadAllBytes("Data/equipment-failure.wav");

        private readonly AudioConfig _audioConfig;

        public readonly AbstractController Controller;

        public SrsAudioClient VoiceHandler;
        public Client SrsClient;

        private TaskCompletionSource<int> _stopRecognition;

        private volatile bool _stop;

        private readonly string _botType;
        private readonly string _callsign;
        private readonly double _frequency;
        
        // Allows OverlordBot to listen for a specific word to start listening. Currently not used although the setup has all been done.
        // This is due to wierd state transition errors that I cannot be bothered to debug. Possible benefit is less calls to Speech endpoint but
        // not sure if that is good enough or not to keep investigating.
        //private readonly KeywordRecognitionModel _wakeWord;

        public SpeechRecognitionListener(BufferedWaveProvider bufferedWaveProvider, ConcurrentQueue<byte[]> responseQueue, RadioInformation radioInfo)
        {
            radioInfo.TransmissionQueue = responseQueue;
            _botType = radioInfo.botType;
            _frequency = radioInfo.freq;
            _callsign = radioInfo.callsign;

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

        public async Task StartListeningAsync()
        {
            while (!_stop) {
                Logger.Debug($"{_logClientId}| Started Continuous Recognition");

                // Initialize the recognizer
                var authorizationToken = SpeechAuthorizationToken.AuthorizationToken;

                var speechConfig =
                    SpeechConfig.FromAuthorizationToken(authorizationToken, Properties.Settings.Default.SpeechRegion);
                speechConfig.EndpointId = Properties.Settings.Default.SpeechCustomEndpointId;
                var recognizer = new SpeechRecognizer(speechConfig, _audioConfig);

                // Setup the cancellation code
                _stopRecognition = new TaskCompletionSource<int>();

                recognizer.Recognized += async (s, e) => { await ProcessRadioCall(e); };

                recognizer.Canceled += (s, e) =>
                {
                    Logger.Trace($"{_logClientId}| CANCELLED: Reason={e.Reason}");

                    if (e.Reason == CancellationReason.Error)
                    {
                        Logger.Trace($"{_logClientId}| CANCELLED: ErrorCode={e.ErrorCode}");
                        Logger.Trace($"{_logClientId}| CANCELLED: ErrorDetails={e.ErrorDetails}");

                        if (e.ErrorCode != CancellationErrorCode.BadRequest &&
                            e.ErrorCode != CancellationErrorCode.ConnectionFailure)
                        {
                            Logger.Trace($"{_logClientId}| Sending Failure Message");
                            Controller.Radio.TransmissionQueue.Enqueue(FailureMessage);
                        }
                    }

                    _stopRecognition.TrySetResult(1);
                };

                recognizer.SpeechStartDetected += (s, e) => { Logger.Trace($"{_logClientId}| Speech started event."); };

                recognizer.SpeechEndDetected += (s, e) => { Logger.Trace($"{_logClientId}| Speech ended event."); };

                recognizer.SessionStarted += (s, e) => { Logger.Trace($"{_logClientId}| Session started event."); };

                recognizer.SessionStopped += (s, e) =>
                {
                    Logger.Trace($"{_logClientId}| Session stopped event.");
                    _stopRecognition.TrySetResult(0);
                };

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                // Waits for completion.
                // Use Task.WaitAny to keep the task rooted.
                Task.WaitAny(_stopRecognition.Task);

                // Stops recognition.
                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
                Logger.Debug($"{_logClientId}| Stopped Continuous Recognition");
            }
        }

        public async Task StopRecognition()
        {
            Logger.Debug($"{_logClientId}| Stopping Continuous Recognition");
            _stop = true;
            _stopRecognition.TrySetResult(0);
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
                Logger.Trace($"{_logClientId}| Sending Failure Message");
                Controller.Radio.TransmissionQueue.Enqueue(FailureMessage);
            }
        }

        private async Task ProcessRecognizedCall(SpeechRecognitionEventArgs e)
        {
            using (var activity = Constants.ActivitySource.StartActivity("ProcessRecognizedCall", ActivityKind.Server))
            {
                Logger.Info($"{_logClientId}| Incoming Transmission: {e.Result.Text}");
                var luisJson = Task.Run(() => LuisService.ParseIntent(e.Result.Text)).Result;
                Logger.Debug($"{_logClientId}| LUIS Response: {luisJson}");

                IRadioCall radioCall = Controller is AtcController ? new AtcRadioCall(luisJson) : new BaseRadioCall(luisJson);

                activity?.AddTag("Frequency", _frequency);
                activity?.AddTag("BotType", _botType);
                activity?.AddTag("Callsign", _callsign);
                activity?.AddTag("Sender", radioCall.Sender?.Callsign);
                activity?.AddTag("Intent", radioCall.Intent);
                activity?.AddTag("Request", radioCall.Message);
                
                var response = Controller.ProcessRadioCall(radioCall);
                activity?.AddTag("Response Text", response);

                if (!string.IsNullOrEmpty(response))
                {
                    Logger.Info($"{_logClientId}| Outgoing Transmission: {response}");
                    var audioResponse = await Task.Run(() => Speaker.CreateResponse(
                        $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{Controller.Voice}\">{response}</voice></speak>"));
                    if (audioResponse == null)
                    {
                        activity?.AddTag("Response", "Failure");
                        activity?.AddEvent(new ActivityEvent("Synthesis Failure"));
                        Logger.Error($"{_logClientId}| Synthesis Failure");
                        using (Constants.ActivitySource.StartActivity("EnqueueResponseAudio", ActivityKind.Producer))
                        {
                            Logger.Trace($"{_logClientId}| Sending Failure Message");
                            Controller.Radio.TransmissionQueue.Enqueue(FailureMessage);
                        }
                    }
                    else
                    {
                        activity?.AddTag("Response", "Success");
                        using (Constants.ActivitySource.StartActivity("EnqueueResponseAudio", ActivityKind.Producer))
                        {
                            Controller.Radio.TransmissionQueue.Enqueue(audioResponse);
                        }
                    }
                }
                else
                {
                    activity?.AddTag("Response", "Ignored");
                    Logger.Info($"{_logClientId}| Radio Call Ignored due to null response for Radio Call Processing");
                }

                if (Controller.Radio.discordTransmissionLogChannelId > 0)
                    LogTransmissionToDiscord(radioCall, response);
            }
        }

        private void LogTransmissionToDiscord(IRadioCall radioCall, string response)
        {
            using (Constants.ActivitySource.StartActivity("LogTransmissionToDiscord"))
            {
                Logger.Debug($"{_logClientId}| Building Discord Request/Response message");
                var transmission = $"Transmission Intent: {radioCall.Intent}\n" +
                                   $"Request: {radioCall.Message}\n" +
                                   $"Response: {response ?? "**IGNORED**"}";
                _ = DiscordClient.LogTransmissionToDiscord(transmission, Controller.Radio, SrsClient).ConfigureAwait(false);
            }
        }
    }
}

using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using FragLabs.Audio.Codecs;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using System.Net.Http;
using System.Threading;
using NLog;
using System.IO;
using NewRelic.Api.Agent;
using System.Collections.Concurrent;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Discord;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;

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

        private readonly string _voice;

        public UdpVoiceHandler _voiceHandler;

        private ConcurrentQueue<byte[]> _responses;

        private RadioInformation _radioInfo;

        public bool TimedOut;

        // Allows OverlordBot to listen for a specific word to start listening. Currently not used although the setup has all been done.
        // This is due to wierd state transition errors thatI cannot be bothered to debug.
        KeywordRecognitionModel _wakeWord;

        public SpeechRecognitionListener(BufferedWaveProvider bufferedWaveProvider, ConcurrentQueue<byte[]> responseQueue, RadioInformation radioInfo)
        {
            _radioInfo = radioInfo;

            _voice = _radioInfo.voice;

            Logger.Debug("VOICE: " + _voice);

           _encoder = OpusEncoder.Create(AudioManager.INPUT_SAMPLE_RATE, 1, FragLabs.Audio.Codecs.Opus.Application.Voip);
           _encoder.ForwardErrorCorrection = false;
           _encoder.FrameByteCount(AudioManager.SEGMENT_FRAMES);

            _streamReader = new BufferedWaveProviderStreamReader(bufferedWaveProvider);
            _audioConfig = AudioConfig.FromStreamInput(_streamReader, AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));

            //_wakeWord = KeywordRecognitionModel.FromFile($"Overlord/WakeWords/{callsign}.table");

            _responses = responseQueue;
        }

        // Gets an authorization token by sending a POST request to the token service.
        public static async Task<string> GetToken()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Settings.SPEECH_SUBSCRIPTION_KEY);
                UriBuilder uriBuilder = new UriBuilder("https://" + Settings.SPEECH_REGION + ".api.cognitive.microsoft.com/sts/v1.0/issueToken");

                using (var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null))
                {
                    if (result.IsSuccessStatusCode)
                    {
                        return await result.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        throw new HttpRequestException($"Cannot get token from {uriBuilder.ToString()}. Error: {result.StatusCode}");
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
            SpeechConfig speechConfig = SpeechConfig.FromAuthorizationToken(authorizationToken, Settings.SPEECH_REGION);
            speechConfig.EndpointId = Settings.SPEECH_CUSTOM_ENDPOINT_ID;
            SpeechRecognizer recognizer = new SpeechRecognizer(speechConfig, _audioConfig);

            // Setup the cancellation code
            var stopRecognition = new TaskCompletionSource<int>();
            CancellationTokenSource source = new CancellationTokenSource();

            // Start the token renewal so we can do long-running recognition.
            var tokenRenewTask = StartTokenRenewTask(source.Token, recognizer);

            recognizer.Recognized += async (s, e) =>
            {
                await ProcessAwacsCall(e);
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
                        _responses.Enqueue(_failureMessage);
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

        [Transaction(Web = true)]
        private async Task ProcessAwacsCall(SpeechRecognitionEventArgs e) {
            string response = null;
            string clientMessage = "";

            try
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    // Send data to the nextgen shadow system. This is not part of the main flow so we don't await.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    //LuisServiceV3.RecognizeAsync(e.Result.Text);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                    Logger.Info($"Incoming Transmission: {e.Result.Text}");
                    string luisJson = Task.Run(() => LuisService.ParseIntent(e.Result.Text)).Result;
                    Logger.Debug($"LIVE LUIS RESPONSE: {luisJson}");
                    LuisResponse luisResponse = JsonConvert.DeserializeObject<LuisResponse>(luisJson);

                    string botCallsign;
                    Player senderInfo = Task.Run(() => SenderExtractor.Extract(luisResponse)).Result;

                    var clientsOnFreq = ConnectedClientsSingleton.Instance.ClientsOnFreq(_radioInfo.freq, RadioInformation.Modulation.AM);
                    foreach (var client in clientsOnFreq)
                    {
                        if (client.Name != "OverlordBot")
                        {
                            clientMessage += $"{client.Name}, ";
                        }
                    }
                    if(clientMessage.Length > 0)
                    {
                        clientMessage = clientMessage.Substring(0, clientMessage.Length - 2);
                    }

                    if (luisResponse.Query != null && luisResponse.TopScoringIntent["intent"] == "None" ||
                        (luisResponse.Entities.Find(x => x.Type == "awacs_callsign") == null &&
                          luisResponse.Entities.Find(x => x.Type == "airbase_caller") == null)
                        )
                    {
                        Logger.Debug($"RESPONSE NO-OP");
                        string transmission = $"Transmission Ignored\nClients on freq: {clientMessage}\nIncoming: " + e.Result.Text;
                        _ = DiscordClient.SendTransmission(transmission).ConfigureAwait(false);
                        // NO-OP
                    }
                    else if (senderInfo == null)
                    {
                        Logger.Debug($"SENDER IS NULL");
                        response = "Last transmitter, I could not recognise your call-sign.";
                    }
                    else
                    {
                        botCallsign = null;
                        if (luisResponse.Entities.Find(x => x.Type == "awacs_callsign") != null)
                        {
                            botCallsign = luisResponse.Entities.Find(x => x.Type == "awacs_callsign").Resolution.Values[0];
                        }
                        else if(luisResponse.Entities.Find(x => x.Type == "airbase") != null)
                        {
                            botCallsign = luisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];
                        }

                        var sender = Task.Run(() => GameQuerier.GetPilotData(senderInfo.Group, senderInfo.Flight, senderInfo.Plane)).Result;

                        if (sender == null)
                        {
                            Logger.Trace($"SenderVerified: false");
                            response = $"{senderInfo.Group} {senderInfo.Flight} {senderInfo.Plane}, {botCallsign}, I cannot find you on scope. ";
                        }
                        else
                        {
                            if (luisResponse.Query != null && (luisResponse.TopScoringIntent["intent"] == "RadioCheck"))
                            {
                                response = $"{sender.Callsign}, {botCallsign}, five by five";
                            }
                            else if (luisResponse.Query != null && luisResponse.TopScoringIntent["intent"] == "BogeyDope")
                            {
                                response = $"{sender.Callsign}, {botCallsign}, ";
                                response += Task.Run(() => BogeyDope.Process(sender)).Result;
                            }
                            else if (luisResponse.Query != null && (luisResponse.TopScoringIntent["intent"] == "BearingToAirbase"))
                            {
                                response = $"{sender.Callsign}, {botCallsign}, ";
                                response += Task.Run(() => BearingToAirbase.Process(luisResponse, sender)).Result;
                            }
                            else if (luisResponse.Query != null && (luisResponse.TopScoringIntent["intent"] == "BearingToFriendlyPlayer"))
                            {
                                response = $"{sender.Callsign}, {botCallsign}, ";
                                response += Task.Run(() => BearingToFriendlyPlayer.Process(luisResponse, sender)).Result;
                            }
                            else if (luisResponse.Query != null && (luisResponse.TopScoringIntent["intent"] == "SetWarningRadius"))
                            {
                                response = $"{sender.Callsign}, {botCallsign}, ";
                                response += Task.Run(() => SetWarningRadius.Process(luisResponse, sender, botCallsign,_voice, _responses)).Result;
                            }
                            else if (luisResponse.Query != null && (luisResponse.TopScoringIntent["intent"] == "Picture"))
                            {
                                response = $"{sender.Callsign}, {botCallsign}, We do not support picture calls ";
                            }
                            else if (luisResponse.Query != null && (luisResponse.TopScoringIntent["intent"] == "Declare"))
                            {
                                response = $"{sender.Callsign}, {botCallsign}, ";
                                response += Task.Run(() => Declare.Process(luisResponse, sender)).Result;
                            }

                            else if (luisResponse.Query != null && (luisResponse.TopScoringIntent["intent"] == "ReadyToTaxi"))
                            {
                                response = $"{sender.Callsign}, {botCallsign} Ground, ";
                                response += Task.Run(() => ReadytoTaxi.Process(botCallsign, sender)).Result;
                            }
                        }
                    }
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Logger.Debug($"NOMATCH: Speech could not be recognized.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error processing radio call");
                _responses.Enqueue(_failureMessage);
                response = null;
            }
            if (response != null)
            {
                Logger.Info($"Outgoing Transmission: {response}");
                string transmission = $"Transmission pair\nClients on freq: {clientMessage}\nIncoming: {e.Result.Text}\nOutgoing: {response}";
                _ = DiscordClient.SendTransmission(transmission).ConfigureAwait(false);
                var audioResponse = await Task.Run(() => Speaker.CreateResponse($"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{response}</voice></speak>"));
                if (audioResponse != null)
                {
                    _responses.Enqueue(audioResponse);
                }
                else
                {
                    _responses.Enqueue(_failureMessage);
                }
            }
        }

        public async Task SendTransmission(string message)
        {
            var audioResponse = await Task.Run(() => Speaker.CreateResponse($"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name =\"{_voice}\">{message}</voice></speak>"));
            _responses.Enqueue(audioResponse);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Easy.MessageHub;
using FragLabs.Audio.Codecs;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using WPFCustomMessageBox;
using Application = FragLabs.Audio.Codecs.Opus.Application;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers
{
    public class AudioManager
    {
        public static readonly int INPUT_SAMPLE_RATE = 16000;

        // public static readonly int OUTPUT_SAMPLE_RATE = 44100;
        public static readonly int INPUT_AUDIO_LENGTH_MS = 40; //TODO test this! Was 80ms but that broke opus

        public static readonly int SEGMENT_FRAMES = (INPUT_SAMPLE_RATE / 1000) * INPUT_AUDIO_LENGTH_MS
            ; //640 is 40ms as INPUT_SAMPLE_RATE / 1000 *40 = 640

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public delegate void VOIPConnectCallback(bool result, bool connectionError, string connection);

        private readonly CachedAudioEffect[] _cachedAudioEffects;

        private readonly ConcurrentDictionary<string, ClientAudioProvider> _clientsBufferedAudio =
            new ConcurrentDictionary<string, ClientAudioProvider>();

        private readonly ConcurrentDictionary<string, RecorderAudioProvider> _recordersBufferedAudio =
            new ConcurrentDictionary<string, RecorderAudioProvider>();

        private readonly ConcurrentDictionary<int, BotAudioProvider> _botsBufferedAudio =
            new ConcurrentDictionary<int, BotAudioProvider>();

        private readonly ConcurrentDictionary<int, ConcurrentQueue<byte[]>> _responseQueue =
            new ConcurrentDictionary<int, ConcurrentQueue<byte[]>>();

        private readonly ConcurrentDictionary<string, SRClient> _clientsList;
        private MixingSampleProvider _clientAudioMixer;

        private OpusDecoder _decoder;

        private OpusEncoder _encoder;

        private UdpVoiceHandler _udpVoiceHandler;

        private VolumeSampleProviderWithPeak _volumeSampleProvider;

        public float MicMax { get; set; } = -100;
        public float SpeakerMax { get; set; } = -100;

        private ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        private readonly SettingsStore _settings = SettingsStore.Instance;

        public AudioManager(ConcurrentDictionary<string, SRClient> clientsList)
        {
            _clientsList = clientsList;

            _cachedAudioEffects =
                new CachedAudioEffect[Enum.GetNames(typeof(CachedAudioEffect.AudioEffectTypes)).Length];
            for (var i = 0; i < _cachedAudioEffects.Length; i++)
            {
                _cachedAudioEffects[i] = new CachedAudioEffect((CachedAudioEffect.AudioEffectTypes) i);
            }
        }

        public float MicBoost { get; set; } = 1.0f;

        public void StartEncoding(int mic, MMDevice speakers, string guid, InputDeviceManager inputManager,
            IPAddress ipAddress, int port, MMDevice micOutput)
        {
            try
            {
                //opus
                _encoder = OpusEncoder.Create(INPUT_SAMPLE_RATE, 1, Application.Voip);
                _encoder.ForwardErrorCorrection = false;
                _decoder = OpusDecoder.Create(INPUT_SAMPLE_RATE, 1);
                _decoder.ForwardErrorCorrection = false;

                //speex
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Output - Quitting! " + ex.Message);  
                ShowOutputError("Problem Initialising Audio Output!");
             
                Environment.Exit(1);
            }

            _udpVoiceHandler = new UdpVoiceHandler(guid, ipAddress, port, _decoder, this);
            var voiceSenderThread = new Thread(_udpVoiceHandler.Listen);

            voiceSenderThread.Start();
        }

        private void ShowInputError(string message)
        {
            if (Environment.OSVersion.Version.Major == 10)
            {
                var messageBoxResult = CustomMessageBox.ShowYesNoCancel(
                    $"{message}\n\n" +
                    $"If you are using Windows 10, this could be caused by your privacy settings (make sure to allow apps to access your microphone)." +
                    $"\nAlternatively, try a different Input device and please post your client log to the support Discord server.",
                    "Audio Input Error",
                    "OPEN PRIVACY SETTINGS",
                    "JOIN DISCORD SERVER",
                    "CLOSE",
                    MessageBoxImage.Error);

                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    Process.Start("ms-settings:privacy-microphone");
                }
                else if (messageBoxResult == MessageBoxResult.No)
                {
                    Process.Start("https://discord.gg/baw7g3t");
                }
            }
            else
            {
                var messageBoxResult = CustomMessageBox.ShowYesNo(
                    $"{message}\n\n" +
                    "Try a different Input device and please post your client log to the support Discord server.",
                    "Audio Input Error",
                    "JOIN DISCORD SERVER",
                    "CLOSE",
                    MessageBoxImage.Error);

                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    Process.Start("https://discord.gg/baw7g3t");
                }
            }
        }

        private void ShowOutputError(string message)
        {
            var messageBoxResult = CustomMessageBox.ShowYesNo(
                $"{message}\n\n" +
                "Try a different output device and please post your client log to the support Discord server.",
                "Audio Output Error",
                "JOIN DISCORD SERVER",
                "CLOSE",
                MessageBoxImage.Error);

            if (messageBoxResult == MessageBoxResult.Yes)
            {
                Process.Start("https://discord.gg/baw7g3t");
            }
        }

        public void PlaySoundEffectStartReceive(int transmitOnRadio, bool encrypted, float volume)
        {
        }

        public void PlaySoundEffectStartTransmit(int transmitOnRadio, bool encrypted, float volume)
        {
        }

        public void PlaySoundEffectEndReceive(int transmitOnRadio, float volume)
        {
            if (_botsBufferedAudio.ContainsKey(transmitOnRadio))
            {
                _botsBufferedAudio[transmitOnRadio].EndTransmission();
            }
        }

        public void PlaySoundEffectEndTransmit(int transmitOnRadio, float volume)
        {
        }

        public void StopEncoding()
        {

            _volumeSampleProvider = null;
            _clientAudioMixer?.RemoveAllMixerInputs();
            _clientAudioMixer = null;

            _clientsBufferedAudio.Clear();
            _recordersBufferedAudio.Clear();

            _encoder?.Dispose();
            _encoder = null;

            _decoder?.Dispose();
            _decoder = null;
          
            if (_udpVoiceHandler != null)
            {
                _udpVoiceHandler.RequestStop();
                _udpVoiceHandler = null;
            }

            SpeakerMax = -100;
            MicMax = -100;

            MessageHub.Instance.ClearSubscriptions();
        }

        public void AddClientAudio(ClientAudio audio)
        {
            BotAudioProvider bot = null;
            if (!_responseQueue.ContainsKey(audio.ReceivedRadio) || _responseQueue[audio.ReceivedRadio] == null)
            {
                _responseQueue[audio.ReceivedRadio] = new ConcurrentQueue<byte[]>();
                CheckForResponses(_responseQueue[audio.ReceivedRadio], audio.ReceivedRadio);
            }

            if (_botsBufferedAudio.ContainsKey(audio.ReceivedRadio) && _botsBufferedAudio[audio.ReceivedRadio].SpeechRecognitionActive() == true)
            {
                bot = _botsBufferedAudio[audio.ReceivedRadio];
            }
            else
            {
                var callsign = _clientStateSingleton.DcsPlayerRadioInfo.radios[audio.ReceivedRadio].name;
                var voice = _clientStateSingleton.DcsPlayerRadioInfo.radios[audio.ReceivedRadio].voice;
                var responseQueue = _responseQueue[audio.ReceivedRadio];
                bot = new BotAudioProvider(callsign, voice, responseQueue);
                bot._speechRecognitionListener._voiceHandler = _udpVoiceHandler;
                _botsBufferedAudio[audio.ReceivedRadio] = bot;
            }
            bot.AddClientAudioSamples(audio);
        }
        private void CheckForResponses(ConcurrentQueue<byte[]> responseQueue, int radioId)
        {
            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (true)
                {
                    byte[] response;
                    if (responseQueue.TryDequeue(out response))
                    {
                        Logger.Trace($"Sending Response: {response}");
                        await SendResponse(response, response.Length, radioId);
                    };
                    Thread.Sleep(50);
                }
            }).Start();
        }

        // Expects a byte buffer containing 16 bit 16KHz 1 channel PCM WAV
        private async Task SendResponse(byte[] buffer, int length, int radioId)
        {
            try
            {
                Queue<byte> audioQueue = new Queue<byte>(length);

                for (var i = 0; i < length; i++)
                {
                    audioQueue.Enqueue(buffer[i]);
                }

                //read out the buffer
                while (audioQueue.Count >= SEGMENT_FRAMES)
                {

                    byte[] packetBuffer = new byte[SEGMENT_FRAMES];

                    for (var i = 0; i < SEGMENT_FRAMES; i++)
                    {
                        if (audioQueue.Count > 0)
                        {
                            packetBuffer[i] = audioQueue.Dequeue();
                        }
                        else
                        {
                            packetBuffer[i] = 0;
                        }
                    }

                    //encode as opus bytes
                    var buff = _encoder.Encode(packetBuffer, SEGMENT_FRAMES, out int len);

                    if ((_udpVoiceHandler != null) && (buff != null) && (len > 0))
                    {
                        //create copy with small buffer
                        var encoded = new byte[len];

                        Buffer.BlockCopy(buff, 0, encoded, 0, len);

                        await Task.Run(() => _udpVoiceHandler.Send(encoded, len, radioId));
                        // Sleep between sending 40ms worth of data so that we do not overflow the 3 second audio buffers of
                        // normal SRS clients. The lower the sleep the less chance of audio corruption due to network issues
                        // but the greater the chance of over-flowing buffers. 20ms sleep per 40ms of audio being sent seems
                        // to be about the right balance.
                        Thread.Sleep(20);
                    }
                    else
                    {
                        Logger.Debug($"Invalid Bytes for Encoding - {length} should be {SEGMENT_FRAMES}");
                    }
                }
                // Send one null to reset the sending state
                await Task.Run(() => _udpVoiceHandler.Send(null, 0, radioId));
                // Sleep for a second between sending messages to give players a chance to split messages.
            } catch (Exception ex)
            {
                Logger.Error(ex, $"Exception sending response. RadioId {radioId}, Response length {length}");
            }
            Thread.Sleep(1000);
        }
    }
}

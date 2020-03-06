using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Threading;
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
            IPAddress ipAddress, int port, MMDevice micOutput, VOIPConnectCallback voipConnectCallback)
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

            _udpVoiceHandler = new UdpVoiceHandler(_clientsList, guid, ipAddress, port, _decoder, this, inputManager, voipConnectCallback);
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
            RecorderAudioProvider recorder = null;
            if (_botsBufferedAudio.ContainsKey(audio.ReceivedRadio))
            {
                bot = _botsBufferedAudio[audio.ReceivedRadio];

            }
            else
            {
                var callsign = _clientStateSingleton.DcsPlayerRadioInfo.radios[audio.ReceivedRadio].name;
                var voice = _clientStateSingleton.DcsPlayerRadioInfo.radios[audio.ReceivedRadio].voice;
                bot = new BotAudioProvider(callsign, voice);
                bot._speechRecognitionListener._voiceHandler = _udpVoiceHandler;
                _botsBufferedAudio[audio.ReceivedRadio] = bot;

            }
            if (_recordersBufferedAudio.ContainsKey(audio.ClientGuid))
            {
                recorder = _recordersBufferedAudio[audio.ClientGuid];
            }
            else
            {
                recorder = new RecorderAudioProvider();
                _recordersBufferedAudio[audio.ClientGuid] = recorder;
            }
            bot.AddClientAudioSamples(audio);
            recorder.AddClientAudioSamples(audio);
        }

        private void RemoveClientBuffer(SRClient srClient)
        {
        }
    }
}

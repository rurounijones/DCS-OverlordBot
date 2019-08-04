using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Utility;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Helpers;
using FragLabs.Audio.Codecs;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Intent;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;
using WPFCustomMessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio
{
    internal class AudioPreview
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private BufferedWaveProvider _playBuffer;
        private WaveIn _waveIn;
        private WasapiOut _waveOut;

        private VolumeSampleProviderWithPeak _volumeSampleProvider;
        private BufferedWaveProvider _requestBufferedWaveProvider;
        private BufferedWaveProvider _responseBufferedWaveProvider;
        
        public float MicBoost { get; set; } = 1.0f;

        private float _speakerBoost = 1.0f;
        private OpusEncoder _encoder;
        private OpusDecoder _decoder;

        private Preprocessor _speex;

        private readonly Queue<byte> _micInputQueue = new Queue<byte>(AudioManager.SEGMENT_FRAMES * 3);
        private WaveFileWriter _waveFile;
        private SettingsStore _settings;

        public float SpeakerBoost
        {
            get { return _speakerBoost; }
            set
            {
                _speakerBoost = value;
                if (_volumeSampleProvider != null)
                {
                    _volumeSampleProvider.Volume = value;
                }
            }
        }

        public float MicMax { get; set; } = -100;
        public float SpeakerMax { get; set; } = -100;

        public void StartPreview(int mic, MMDevice speakers)
        {
            try
            {

                WaveFormat pcm = new WaveFormat(AudioManager.INPUT_SAMPLE_RATE, 16, 1);

                _requestBufferedWaveProvider =
                    new BufferedWaveProvider(pcm)
                    {
                        ReadFully = false,
                        DiscardOnBufferOverflow = true
                    };

                _responseBufferedWaveProvider =
                    new BufferedWaveProvider(pcm)
                    {
                        ReadFully = true,
                        DiscardOnBufferOverflow = true
                    };
                // So far our Bogey Dope calls take less than 10 seconds sooo...
                _responseBufferedWaveProvider.BufferLength = pcm.AverageBytesPerSecond * 10;

                // Creates an instance of a speech config with specified subscription key
                // and service region. Note that in contrast to other services supported by
                // the Cognitive Services Speech SDK, the Language Understanding service
                // requires a specific subscription key from https://www.luis.ai/.
                // The Language Understanding service calls the required key 'endpoint key'.
                // Once you've obtained it, replace with below with your own Language Understanding subscription key
                // and service region (e.g., "westus").
                // The default language is "en-us".
                var luisConfig = SpeechConfig.FromSubscription("cdf044178ef94f3e86ff37d6967cb507", "westus");

                var audioInput = AudioConfig.FromStreamInput(new RadioStreamReader(_requestBufferedWaveProvider));
                var listener = new RadioListener(new IntentRecognizer(luisConfig, audioInput), _responseBufferedWaveProvider);
                Task.Run(() => listener.StartListeningAsync());


                // Start the output processing!

                _settings = SettingsStore.Instance;
                _waveOut = new WasapiOut(speakers, AudioClientShareMode.Shared, true, 40);

                RadioFilter filter = new RadioFilter(_responseBufferedWaveProvider.ToSampleProvider());

                //add final volume boost to all mixed audio
                _volumeSampleProvider = new VolumeSampleProviderWithPeak(filter,
                    (peak => SpeakerMax = (float)VolumeConversionHelper.ConvertFloatToDB(peak)))
                {
                    Volume = SpeakerBoost
                };

                if (speakers.AudioClient.MixFormat.Channels == 1)
                {
                    if (_volumeSampleProvider.WaveFormat.Channels == 2)
                    {
                        _waveOut.Init(_volumeSampleProvider.ToMono());
                    }
                    else
                    {
                        //already mono
                        _waveOut.Init(_volumeSampleProvider);
                    }
                }
                else
                {
                    if (_volumeSampleProvider.WaveFormat.Channels == 1)
                    {
                        _waveOut.Init(_volumeSampleProvider.ToStereo());
                    }
                    else
                    {
                        //already stereo
                        _waveOut.Init(_volumeSampleProvider);
                    }
                }

                _waveOut.Play();

            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Output - Quitting! " + ex.Message);

                ShowOutputError("Problem Initialising Audio Output!");

                Environment.Exit(1);
            }

            try
            {
                _speex = new Preprocessor(AudioManager.SEGMENT_FRAMES, AudioManager.INPUT_SAMPLE_RATE);
                //opus
                _encoder = OpusEncoder.Create(AudioManager.INPUT_SAMPLE_RATE, 1,
                    FragLabs.Audio.Codecs.Opus.Application.Voip);
                _encoder.ForwardErrorCorrection = false;
                _decoder = OpusDecoder.Create(AudioManager.INPUT_SAMPLE_RATE, 1);
                _decoder.ForwardErrorCorrection = false;

                _waveIn = new WaveIn(WaveCallbackInfo.FunctionCallback())
                {
                    BufferMilliseconds = AudioManager.INPUT_AUDIO_LENGTH_MS,
                    DeviceNumber = mic
                };

                _waveIn.NumberOfBuffers = 2;
                _waveIn.DataAvailable += _waveIn_DataAvailable;
                _waveIn.WaveFormat = new WaveFormat(AudioManager.INPUT_SAMPLE_RATE, 16, 1);

                //debug wave file
                //_waveFile = new WaveFileWriter(@"C:\Temp\Test-Preview.wav", _waveIn.WaveFormat);

                _waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Input - Quitting! " + ex.Message);
                ShowInputError();

                Environment.Exit(1);
            }
        }

        private void ShowInputError()
        {
            if (Environment.OSVersion.Version.Major == 10)
            {
                var messageBoxResult = CustomMessageBox.ShowYesNoCancel(
                    "Problem initialising Audio Input!\n\nIf you are using Windows 10, this could be caused by your privacy settings (make sure to allow apps to access your microphone).\nAlternatively, try a different Input device and please post your client log to the support Discord server.",
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
                    "Problem initialising Audio Input!\n\nTry a different Input device and please post your client log to the support Discord server.",
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

        private void _waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            //fill sound buffer

            short[] pcmShort = null;


            if ((e.BytesRecorded / 2 == AudioManager.SEGMENT_FRAMES) && (_micInputQueue.Count == 0))
            {
                //perfect!
                pcmShort = new short[AudioManager.SEGMENT_FRAMES];
                Buffer.BlockCopy(e.Buffer, 0, pcmShort, 0, e.BytesRecorded);
            }
            else
            {
                for (var i = 0; i < e.BytesRecorded; i++)
                {
                    _micInputQueue.Enqueue(e.Buffer[i]);
                }
            }

            //read out the queue
            while ((pcmShort != null) || (_micInputQueue.Count >= AudioManager.SEGMENT_FRAMES))
            {
                //null sound buffer so read from the queue
                if (pcmShort == null)
                {
                    pcmShort = new short[AudioManager.SEGMENT_FRAMES];

                    for (var i = 0; i < AudioManager.SEGMENT_FRAMES; i++)
                    {
                        pcmShort[i] = _micInputQueue.Dequeue();
                    }
                }

                try
                {
                    //process with Speex
                    _speex.Process(new ArraySegment<short>(pcmShort));

                    float max = 0;
                    for (var i = 0; i < pcmShort.Length; i++)
                    {


                        //determine peak
                        if (pcmShort[i] > max)
                        {

                            max = pcmShort[i];

                        }
                    }
                    //convert to dB
                    MicMax = (float)VolumeConversionHelper.ConvertFloatToDB(max / 32768F);

                    var pcmBytes = new byte[pcmShort.Length * 2];
                    Buffer.BlockCopy(pcmShort, 0, pcmBytes, 0, pcmBytes.Length);

   //                 _requestBufferedWaveProvider.AddSamples(pcmBytes, 0, pcmBytes.Length);
                    //encode as opus bytes
                    int len;
                    //need to get framing right for opus - 
                    var buff = _encoder.Encode(pcmBytes, pcmBytes.Length, out len);

                    if ((buff != null) && (len > 0))
                    {
                        //create copy with small buffer
                        var encoded = new byte[len];

                        Buffer.BlockCopy(buff, 0, encoded, 0, len);

                        var decodedLength = 0;
                        //now decode
                        var decodedBytes = _decoder.Decode(encoded, len, out decodedLength);

                        _requestBufferedWaveProvider.AddSamples(decodedBytes, 0, decodedLength);

                        //_waveFile.Write(decodedBytes, 0,decodedLength);
                       // _waveFile.Flush();
                    }
                    else
                    {
                        Logger.Error(
                            $"Invalid Bytes for Encoding - {e.BytesRecorded} should be {AudioManager.SEGMENT_FRAMES} ");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error encoding Opus! " + ex.Message);
                }

                pcmShort = null;
            }
        }

        public void StopEncoding()
        {
            _waveIn?.Dispose();
            _waveIn = null;
       
            _waveOut?.Dispose();
            _waveOut = null;
        
            _playBuffer?.ClearBuffer();
            _playBuffer = null;
          
            _encoder?.Dispose();
            _encoder = null;
     
            _decoder?.Dispose();
            _decoder = null;
         
            _playBuffer?.ClearBuffer();
            _playBuffer = null;

            _speex?.Dispose();
            _speex = null;

            _waveFile?.Dispose();
            _waveFile = null;

            SpeakerMax = -100;
            MicMax = -100;
        }
    }
}
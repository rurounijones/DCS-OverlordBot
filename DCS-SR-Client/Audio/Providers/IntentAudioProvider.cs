using System;
using System.Diagnostics;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using MathNet.Filtering;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using FragLabs.Audio.Codecs;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class IntentAudioProvider : AudioProvider
    {
        public static readonly int SILENCE_PAD = 300;

        private readonly Random _random = new Random();

        private int _lastReceivedOn = -1;
        private OnlineFilter[] _filters;

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;

        private OpusDecoder _decoder;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public IntentAudioProvider()
        {
            _filters = new OnlineFilter[2];
            _filters[0] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.INPUT_SAMPLE_RATE, 560, 3900);
            _filters[1] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.INPUT_SAMPLE_RATE, 100, 4500);

            JitterBufferProviderInterface =
                new JitterBufferProviderInterface(new WaveFormat(AudioManager.INPUT_SAMPLE_RATE, 2));

            SampleProvider = new Pcm16BitToSampleProvider(JitterBufferProviderInterface);

            _decoder = OpusDecoder.Create(AudioManager.INPUT_SAMPLE_RATE, 1);
            _decoder.ForwardErrorCorrection = false;

            _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.INPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.INPUT_SAMPLE_RATE, 4130, 2.0f);
        }

        public JitterBufferProviderInterface JitterBufferProviderInterface { get; }
        public Pcm16BitToSampleProvider SampleProvider { get; }

        public long LastUpdate { get; private set; }

        //is it a new transmission?
        public bool LikelyNewTransmission()
        {
            //400 ms since last update
            long now = DateTime.Now.Ticks;
            if ((now - LastUpdate) > 4000000) //400 ms since last update
            {
                return true;
            }

            return false;
        }

        public void AddClientAudioSamples(ClientAudio audio, BufferedWaveProvider radioBuffer)
        {

            bool newTransmission = LikelyNewTransmission();

            int decodedLength = 0;

            var decoded = _decoder.Decode(audio.EncodedAudio,
                audio.EncodedAudio.Length, out decodedLength, newTransmission);

            if (decodedLength > 0)
            {

                // for some reason if this is removed then it lags?!
                //guess it makes a giant buffer and only uses a little?
                //Answer: makes a buffer of 4000 bytes - so throw away most of it
                var tmp = new byte[decodedLength];
                Buffer.BlockCopy(decoded, 0, tmp, 0, decodedLength);

                audio.PcmAudioShort = ConversionHelpers.ByteArrayToShortArray(tmp);

                if (newTransmission)
                {
                    // System.Diagnostics.Debug.WriteLine(audio.ClientGuid+"ADDED");
                    //append ms of silence - this functions as our jitter buffer??
                    var silencePad = (AudioManager.INPUT_SAMPLE_RATE / 1000) * SILENCE_PAD;

                    var newAudio = new short[audio.PcmAudioShort.Length + silencePad];

                    Buffer.BlockCopy(audio.PcmAudioShort, 0, newAudio, silencePad, audio.PcmAudioShort.Length);

                    audio.PcmAudioShort = newAudio;
                }

                _lastReceivedOn = audio.ReceivedRadio;
                LastUpdate = DateTime.Now.Ticks;

                radioBuffer.AddSamples(ConversionHelpers.ShortArrayToByteArray(audio.PcmAudioShort), 0, audio.PcmAudioShort.Length);

                //timer.Stop();
            }
            else
            {
                Logger.Info("Failed to decode audio from Packet for client");
            }
        }

        private short RandomShort()
        {
            //random short at max volume at eights
            return (short)_random.Next(-32768 / 8, 32768 / 8);
        }

        //destructor to clear up opus
        ~IntentAudioProvider()
        {
            _decoder.Dispose();
            _decoder = null;
        }

    }
}
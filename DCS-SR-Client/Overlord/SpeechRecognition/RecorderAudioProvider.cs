using System;
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
    public class RecorderAudioProvider : AudioProvider
    {
        public static readonly int SILENCE_PAD = 300;

        private readonly Random _random = new Random();

        private int _lastReceivedOn = -1;

        private OpusDecoder _decoder;

        private WaveFileWriter _waveFileWriter;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public RecorderAudioProvider()
        {
            _decoder = OpusDecoder.Create(AudioManager.INPUT_SAMPLE_RATE, 1);
            _decoder.ForwardErrorCorrection = false;

            _waveFileWriter = new WaveFileWriter($"E:\\Recordings\\{Guid.NewGuid()}.wav", new WaveFormat(16000, 1));
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

        public void AddClientAudioSamples(ClientAudio audio)
        {
            bool newTransmission = LikelyNewTransmission();

            if (newTransmission && _waveFileWriter != null)
            {
                _waveFileWriter.Close();
                _waveFileWriter = new WaveFileWriter($"E:\\Recordings\\{Guid.NewGuid()}.wav", new WaveFormat(16000, 1));
            }

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

                var pcmAudio = ConversionHelpers.ShortArrayToByteArray(audio.PcmAudioShort);

                _waveFileWriter.Write(pcmAudio, 0, pcmAudio.Length);
                _waveFileWriter.Flush();
            }
            else
            {
                Logger.Info("Failed to decode audio from Packet for recorder");
            }
        }

        //destructor to clear up opus
        ~RecorderAudioProvider()
        {
            _decoder.Dispose();
            _decoder = null;

            _waveFileWriter.Close();
            _waveFileWriter.Dispose();
        }

    }
}
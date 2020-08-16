using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NAudio.Wave;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    public class BotAudioProvider : AudioProvider
    {

        //https://trac.ffmpeg.org/wiki/audio%20types
        public static readonly WaveFormat PCM_MONO_16K_S16LE = new WaveFormat(16000, 1);

        readonly BufferedWaveProvider _speechAudioProvider;
        public SpeechRecognitionListener SpeechRecognitionListener { get; set; }
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public double Frequency;

        public BotAudioProvider(RadioInformation receivedRadioInfo, ConcurrentQueue<byte[]> responseQueue)
        {
            _speechAudioProvider = new BufferedWaveProvider(PCM_MONO_16K_S16LE)
            {
                BufferDuration = new TimeSpan(0, 1, 0),
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };

            var callsign = receivedRadioInfo.name;
            var voice = receivedRadioInfo.voice;
            Frequency = receivedRadioInfo.freq;

            SpeechRecognitionListener = new SpeechRecognitionListener(_speechAudioProvider, responseQueue, receivedRadioInfo);
            Task.Run(() => SpeechRecognitionListener.StartListeningAsync());
        }

        public async Task SendTransmission(string message)
        {
            await SpeechRecognitionListener.SendTransmission(message);
        }

        public bool SpeechRecognitionActive()
        {
            return SpeechRecognitionListener.TimedOut == false;
        }

        public void AddClientAudioSamples(ClientAudio audio)
        {
            var newTransmission = LikelyNewTransmission();

            var decoded = _decoder.Decode(audio.EncodedAudio,
                audio.EncodedAudio.Length, out var decodedLength, newTransmission);

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

                LastUpdate = DateTime.Now.Ticks;

                var pcmAudio = ConversionHelpers.ShortArrayToByteArray(audio.PcmAudioShort);
                _speechAudioProvider.AddSamples(pcmAudio, 0, pcmAudio.Length);
            }
            else
            {
                Logger.Debug("Failed to decode audio from Packet for client");
            }
        }

        public void EndTransmission()
        {
            var silence = new byte[AudioManager.INPUT_SAMPLE_RATE / 1000 * 2000];
            _speechAudioProvider.AddSamples(silence, 0, silence.Length);
        }

        //destructor to clear up opus
        ~BotAudioProvider()
        {
            _decoder.Dispose();
            _decoder = null;
        }

    }
}

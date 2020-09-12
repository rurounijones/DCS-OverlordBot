using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NAudio.Wave;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers
{
    public class BotAudioProvider : AudioProvider
    {

        //https://trac.ffmpeg.org/wiki/audio%20types
        public static readonly WaveFormat PcmMono16Ks16Le = new WaveFormat(16000, 1);

        private readonly BufferedWaveProvider _speechAudioProvider;
        public SpeechRecognitionListener SpeechRecognitionListener { get; set; }
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly byte[] Silence;

        public BotAudioProvider(RadioInformation receivedRadioInfo, ConcurrentQueue<byte[]> responseQueue)
        {
            _speechAudioProvider = new BufferedWaveProvider(PcmMono16Ks16Le)
            {
                BufferDuration = new TimeSpan(0, 1, 0),
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };
            Silence =  new byte[_speechAudioProvider.WaveFormat.AverageBytesPerSecond * 2];
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

            var decoded = Decoder.Decode(audio.EncodedAudio,
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
                    var silencePad = AudioManager.InputSampleRate / 1000 * SilencePad;

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
            _speechAudioProvider.AddSamples(Silence, 0, Silence.Length);
        }

        //destructor to clear up opus
        ~BotAudioProvider()
        {
            Decoder.Dispose();
            Decoder = null;
        }

    }
}

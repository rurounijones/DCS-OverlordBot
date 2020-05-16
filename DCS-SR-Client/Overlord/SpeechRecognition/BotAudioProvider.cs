using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NAudio.Wave;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    public class BotAudioProvider : AudioProvider
    {

        //https://trac.ffmpeg.org/wiki/audio%20types
        public static readonly WaveFormat PCM_MONO_16K_S16LE = new WaveFormat(16000, 1);

        BufferedWaveProvider _SpeechAudioProvider;
        public SpeechRecognitionListener _speechRecognitionListener { get; set; }
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        public BotAudioProvider(RadioInformation recievedRadioInfo, ConcurrentQueue<byte[]> responseQueue)
        {
            _SpeechAudioProvider = new BufferedWaveProvider(PCM_MONO_16K_S16LE)
            {
                BufferDuration = new TimeSpan(0, 1, 0),
                DiscardOnBufferOverflow = true,
                ReadFully = false
            };

            var callsign = recievedRadioInfo.name;
            var voice = recievedRadioInfo.voice;

            _speechRecognitionListener = new SpeechRecognitionListener(_SpeechAudioProvider, responseQueue, recievedRadioInfo);
            Task.Run(() => _speechRecognitionListener.StartListeningAsync());
        }

        public bool SpeechRecognitionActive()
        {
            return _speechRecognitionListener.TimedOut == false;
        }

        public void AddClientAudioSamples(ClientAudio audio)
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

                LastUpdate = DateTime.Now.Ticks;

                var pcmAudio = ConversionHelpers.ShortArrayToByteArray(audio.PcmAudioShort);
                _SpeechAudioProvider.AddSamples(pcmAudio, 0, pcmAudio.Length);
            }
            else
            {
                Logger.Debug("Failed to decode audio from Packet for client");
            }
        }

        public void EndTransmission()
        {
            var silence = new byte[AudioManager.INPUT_SAMPLE_RATE / 1000 * 2000];
            _SpeechAudioProvider.AddSamples(silence, 0, silence.Length);
        }

        //destructor to clear up opus
        ~BotAudioProvider()
        {
            _decoder.Dispose();
            _decoder = null;
        }

    }
}

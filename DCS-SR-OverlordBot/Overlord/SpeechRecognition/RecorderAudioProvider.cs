using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NAudio.Wave;
using NLog;
using System;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class RecorderAudioProvider : AudioProvider
    {
        private WaveFileWriter _waveFileWriter;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public void AddClientAudioSamples(ClientAudio audio)
        {
            bool newTransmission = LikelyNewTransmission();

            if (_waveFileWriter == null)
            {
                _waveFileWriter = new WaveFileWriter($"recordings/{Guid.NewGuid()}.wav", new WaveFormat(16000, 1));
            }
            else if (newTransmission)
            {
                _waveFileWriter.Close();
                _waveFileWriter = new WaveFileWriter($"recordings/{Guid.NewGuid()}.wav", new WaveFormat(16000, 1));
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
                Logger.Debug("Failed to decode audio from Packet for recorder");
            }
        }

        //destructor to clear up opus
        ~RecorderAudioProvider()
        {
            _decoder.Dispose();
            _decoder = null;

            if (_waveFileWriter != null)
            {
                _waveFileWriter.Close();
                _waveFileWriter.Dispose();
            }
        }

    }
}
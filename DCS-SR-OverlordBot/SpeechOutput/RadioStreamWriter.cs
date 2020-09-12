using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace RurouniJones.DCS.OverlordBot.SpeechOutput
{
    internal class RadioStreamWriter : PushAudioOutputStreamCallback
    {
        private BufferedWaveProvider _provider;

        public RadioStreamWriter(BufferedWaveProvider provider)
        {
            _provider = provider;
        }

        public override uint Write(byte[] dataBuffer)
        {
            return 0;
        }

        protected override void Dispose(bool disposing)
        {
        }

    }
}
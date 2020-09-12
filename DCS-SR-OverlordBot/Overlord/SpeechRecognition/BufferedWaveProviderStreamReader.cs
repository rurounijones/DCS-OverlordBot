using System.Threading;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    public class BufferedWaveProviderStreamReader : PullAudioInputStreamCallback
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private BufferedWaveProvider _provider;

        public BufferedWaveProviderStreamReader(BufferedWaveProvider provider)
        {
            _provider = provider;
        }

        public override int Read(byte[] dataBuffer, uint size)
        {
            // PullAudioInputStreamCallback classes are expect to block on read 
            // however BufferedWaveProvider do not. therefore we will block until
            // the BufferedWaveProvider has something to return.
            while (_provider.BufferedBytes == 0) { Thread.Sleep(50); }
            return _provider.Read(dataBuffer, 0, (int)size);
        }

        protected override void Dispose(bool disposing)
        {
            _provider.ClearBuffer();
            _provider = null;
            base.Dispose(disposing);
        }
    }
}
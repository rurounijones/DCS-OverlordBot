using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechRecognition
{
    public class BufferedWaveProviderStreamReader : PullAudioInputStreamCallback
    {
        private BufferedWaveProvider _provider;
        private bool _endTransmission = false;

        public BufferedWaveProviderStreamReader(BufferedWaveProvider provider)
        {
            _provider = provider;
        }

        public override int Read(byte[] dataBuffer, uint size)
        {
            // PullAudioInputStreamCallback classes are expect to block on read 
            // however BufferedWaveProvider do not. therefore we will block until
            // the BufferedWaveProvider has something to return.
            while (_provider.BufferedBytes == 0 && _endTransmission == false) { Thread.Sleep(50); }
            return _provider.Read(dataBuffer, 0, (int)size);
        }

        protected override void Dispose(bool disposing)
        {
            _provider.ClearBuffer();
            _provider = null;
            base.Dispose(disposing);
        }

        public void EndTransmission()
        {
            _endTransmission = true;
        }
    }
}
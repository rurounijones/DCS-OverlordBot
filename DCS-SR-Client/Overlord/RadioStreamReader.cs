using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    public class RadioStreamReader : PullAudioInputStreamCallback
    {
        private BufferedWaveProvider _provider;

        public RadioStreamReader(BufferedWaveProvider provider)
        {
            _provider = provider;
        }

        public override int Read(byte[] dataBuffer, uint size)
        {
            while (_provider.BufferedBytes == 0) { Thread.Sleep(100); }
            return _provider.Read(dataBuffer, 0, (int)size);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}

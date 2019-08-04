using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    public class RadioStreamWriter : PushAudioOutputStreamCallback
    {
        private BufferedWaveProvider _provider;

        /// <summary>
        /// Constructor
        /// </summary>
        public RadioStreamWriter(BufferedWaveProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// A callback which is invoked when the synthesizer has a output audio chunk to write out
        /// </summary>
        /// <param name="dataBuffer">The output audio chunk sent by synthesizer</param>
        /// <returns>Tell synthesizer how many bytes are received</returns>
        public override uint Write(byte[] dataBuffer)
        {
            _provider.AddSamples(dataBuffer, 0, dataBuffer.Length);
            Console.WriteLine(_provider.BufferedBytes);

            Console.WriteLine($"{dataBuffer.Length} bytes received.");
            return (uint)dataBuffer.Length;
        }

        /// <summary>
        /// A callback which is invoked when the synthesizer is about to close the stream
        /// </summary>
        public override void Close()
        {
            Console.WriteLine("Push audio output stream closed.");
        }
    }
}

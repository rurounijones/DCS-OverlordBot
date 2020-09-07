using System;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using FragLabs.Audio.Codecs;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio
{
    public abstract class AudioProvider
    {
        protected readonly SettingsStore Settings;

        public static readonly int SilencePad = 300;

        public OpusDecoder Decoder { get; protected set; }

        // Timestamp of the last update
        public long LastUpdate { get; protected set; }

        protected AudioProvider()
        {
            Decoder = OpusDecoder.Create(AudioManager.InputSampleRate, 1);
            Decoder.ForwardErrorCorrection = false;

            Settings = SettingsStore.Instance;
        }

        //is it a new transmission?
        public bool LikelyNewTransmission()
        {
            //400 ms since last update
            var now = DateTime.Now.Ticks;
            return now - LastUpdate > 4000000;
        }
    }
}
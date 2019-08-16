using System;
using System.Diagnostics;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using MathNet.Filtering;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Ciribob.DCS.SimpleRadio.Standalone.Client.DSP;
using FragLabs.Audio.Codecs;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class ClientAudioProvider : AudioProvider
    {
        private readonly Random _random = new Random();

        private OnlineFilter[] _filters;

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public ClientAudioProvider()
        {
            _filters = new OnlineFilter[2];
            _filters[0] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.INPUT_SAMPLE_RATE, 560, 3900);
            _filters[1] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.INPUT_SAMPLE_RATE, 100, 4500);

            JitterBufferProviderInterface =
                new JitterBufferProviderInterface(new WaveFormat(AudioManager.INPUT_SAMPLE_RATE, 2));

            SampleProvider = new Pcm16BitToSampleProvider(JitterBufferProviderInterface);

            _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.INPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.INPUT_SAMPLE_RATE, 4130, 2.0f);
        }

        public JitterBufferProviderInterface JitterBufferProviderInterface { get; }
        public Pcm16BitToSampleProvider SampleProvider { get; }

        public void AddClientAudioSamples(ClientAudio audio)
        {
            //sort out volume
//            var timer = new Stopwatch();
//            timer.Start();

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

                var decrytable = audio.Decryptable || (audio.Encryption == 0);

                if (decrytable)
                {
                    //adjust for LOS + Distance + Volume
                    AdjustVolume(audio);
                         
                    if (_settings.GetClientSetting(SettingsKeys.RadioEffects).BoolValue)
                    {
                        if (audio.ReceivedRadio == 0)
                        {
                            AddRadioEffectIntercom(audio);
                        }
                        else
                        {
                            AddRadioEffect(audio);
                        }
                    }
                
                }
                else
                {
                    AddEncryptionFailureEffect(audio);

                    if (_settings.GetClientSetting(SettingsKeys.RadioEffects).BoolValue)
                    {
                        AddRadioEffect(audio);
                    }
                }

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

                JitterBufferProviderInterface.AddSamples(new JitterBufferAudio
                {
                    Audio =
                        SeperateAudio(ConversionHelpers.ShortArrayToByteArray(audio.PcmAudioShort),
                            audio.ReceivedRadio),
                    PacketNumber = audio.PacketNumber
                });

                //timer.Stop();
            }
            else
            {
                Logger.Info("Failed to decode audio from Packet for client");
            }
        }

        private void AddRadioEffectIntercom(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                var audio = mixedAudio[i] / 32768f;

                audio = _highPassFilter.Transform(audio);

                if (float.IsNaN(audio))
                    audio = _lowPassFilter.Transform(mixedAudio[i]);
                else
                    audio = _lowPassFilter.Transform(audio);

                if (!float.IsNaN(audio))
                {
                    // clip
                    if (audio > 1.0f)
                        audio = 1.0f;
                    if (audio < -1.0f)
                        audio = -1.0f;

                    mixedAudio[i] = (short)(audio * 32767);
                }
            }
            
        }

        private void AdjustVolume(ClientAudio clientAudio)
        {
            var audio = clientAudio.PcmAudioShort;
            for (var i = 0; i < audio.Length; i++)
            {
                var speaker1Short = (short) (audio[i] * clientAudio.Volume);

                //add in radio loss
                //if less than loss reduce volume
                if (clientAudio.RecevingPower > 0.85) // less than 20% or lower left
                {
                    //gives linear signal loss from 15% down to 0%
                    speaker1Short = (short)(speaker1Short * (1.0f - clientAudio.RecevingPower));
                }

                //0 is no loss so if more than 0 reduce volume
                if (clientAudio.LineOfSightLoss > 0)
                {
                    speaker1Short = (short) (speaker1Short * (1.0f - clientAudio.LineOfSightLoss));
                }

                audio[i] = speaker1Short;
            }
        }


        private void AddRadioEffect(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                var audio = (double) mixedAudio[i] / 32768f;

                if (_settings.GetClientSetting(SettingsKeys.RadioEffectsClipping).BoolValue)
                {
                    if (audio > RadioFilter.CLIPPING_MAX)
                    {
                        audio = RadioFilter.CLIPPING_MAX;
                    }
                    else if (audio < RadioFilter.CLIPPING_MIN)
                    {
                        audio = RadioFilter.CLIPPING_MIN;
                    }
                }

                //high and low pass filter
                for (int j = 0; j < _filters.Length; j++)
                {
                    var filter = _filters[j];
                    audio = filter.ProcessSample(audio);

                    if (double.IsNaN(audio))
                        audio = (double) mixedAudio[j] / 32768f;
                    else
                    {
                        // clip
                        if (audio > 1.0f)
                            audio = 1.0f;
                        if (audio < -1.0f)
                            audio = -1.0f;
                    }
                }

                mixedAudio[i] = (short) (audio * 32767);
            }
        }

        private void AddEncryptionFailureEffect(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                mixedAudio[i] = RandomShort();
            }
        }

        private short RandomShort()
        {
            //random short at max volume at eights
            return (short) _random.Next(-32768 / 8, 32768 / 8);
        }

        //destructor to clear up opus
        ~ClientAudioProvider()
        {
            _decoder.Dispose();
            _decoder = null;
        }
       
    }
}
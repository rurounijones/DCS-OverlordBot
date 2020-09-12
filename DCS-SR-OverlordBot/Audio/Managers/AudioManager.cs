using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Providers;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Easy.MessageHub;
using FragLabs.Audio.Codecs;
using FragLabs.Audio.Codecs.Opus;
using NAudio.Wave.SampleProviders;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers
{
    public class AudioManager
    {
        public static readonly int InputSampleRate = 16000;

        // public static readonly int OUTPUT_SAMPLE_RATE = 44100;
        public static readonly int InputAudioLengthMs = 40; //TODO test this! Was 80ms but that broke opus

        public static readonly int SegmentFrames = InputSampleRate / 1000 * InputAudioLengthMs
            ; //640 is 40ms as INPUT_SAMPLE_RATE / 1000 *40 = 640

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public BotAudioProvider BotAudioProvider;
        public readonly ConcurrentQueue<byte[]> ResponseQueue = new ConcurrentQueue<byte[]>();

        private MixingSampleProvider _clientAudioMixer;

        private OpusDecoder _decoder;

        private OpusEncoder _encoder;

        public float MicMax { get; set; } = -100;
        public float SpeakerMax { get; set; } = -100;

        public readonly Network.Client Client;

        public AudioManager(DCSPlayerRadioInfo playerRadioInfo)
        {
            Client = new Network.Client(this, playerRadioInfo);
        }

        public void ConnectToSRS(IPEndPoint address)
        {
            Client.ConnectData(address);
        }

        public void StartEncoding()
        {
            BotAudioProvider = new BotAudioProvider(Client.DcsPlayerRadioInfo.radios[0], ResponseQueue)
            {
                SpeechRecognitionListener = { VoiceHandler = Client.UdpVoiceHandler }
            };
            StartResponseCheckLoop();

            try
            {
                //opus
                _encoder = OpusEncoder.Create(InputSampleRate, 1, Application.Voip);
                _encoder.ForwardErrorCorrection = false;
                _decoder = OpusDecoder.Create(InputSampleRate, 1);
                _decoder.ForwardErrorCorrection = false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting audio Output - Quitting! " + ex.Message);  
             
                Environment.Exit(1);
            }
        }

        public void StopEncoding()
        {
            Client.Disconnect();
            _clientAudioMixer?.RemoveAllMixerInputs();
            _clientAudioMixer = null;

            _encoder?.Dispose();
            _encoder = null;

            _decoder?.Dispose();
            _decoder = null;
          
            if (Client.UdpVoiceHandler != null)
            {
                Client.UdpVoiceHandler.RequestStop();
                Client.UdpVoiceHandler = null;
            }

            SpeakerMax = -100;
            MicMax = -100;

            MessageHub.Instance.ClearSubscriptions();
        }

        public void AddClientAudio(ClientAudio audio)
        {
            BotAudioProvider.AddClientAudioSamples(audio);
        }

        private void StartResponseCheckLoop()
        {
            new Thread(async () =>
            {
                Thread.CurrentThread.IsBackground = true;
                while (true)
                {
                    if (ResponseQueue.TryDequeue(out var response) && response != null)
                    {
                        Logger.Trace($"Sending Response: {response}");
                        await SendResponse(response, response.Length);
                    }
                    Thread.Sleep(50);
                }
            }) {Name = "Audio Sender"}.Start();
        }

        public void EndTransmission()
        {
            BotAudioProvider.EndTransmission();
        }

        // Expects a byte buffer containing 16 bit 16KHz 1 channel PCM WAV
        private async Task SendResponse(IReadOnlyList<byte> buffer, int length)
        {
            try
            {
                var audioQueue = new Queue<byte>(length);

                for (var i = 0; i < length; i++)
                {
                    audioQueue.Enqueue(buffer[i]);
                }

                //read out the buffer
                while (audioQueue.Count >= SegmentFrames)
                {

                    var packetBuffer = new byte[SegmentFrames];

                    for (var i = 0; i < SegmentFrames; i++)
                    {
                        if (audioQueue.Count > 0)
                        {
                            packetBuffer[i] = audioQueue.Dequeue();
                        }
                        else
                        {
                            packetBuffer[i] = 0;
                        }
                    }

                    //encode as opus bytes
                    var buff = _encoder.Encode(packetBuffer, SegmentFrames, out var len);

                    if (Client.UdpVoiceHandler != null && buff != null && len > 0)
                    {
                        //create copy with small buffer
                        var encoded = new byte[len];

                        Buffer.BlockCopy(buff, 0, encoded, 0, len);

                        await Task.Run(() => Client.UdpVoiceHandler.Send(encoded, len, 0));
                        // Sleep between sending 40ms worth of data so that we do not overflow the 3 second audio buffers of
                        // normal SRS clients. The lower the sleep the less chance of audio corruption due to network issues
                        // but the greater the chance of over-flowing buffers. 20ms sleep per 40ms of audio being sent seems
                        // to be about the right balance.
                        Thread.Sleep(20);
                    }
                    else
                    {
                        Logger.Debug($"Invalid Bytes for Encoding - {length} should be {SegmentFrames}");
                    }
                }
                // Send one null to reset the sending state
                await Task.Run(() => Client.UdpVoiceHandler.Send(null, 0, 0));
                // Sleep for a second between sending messages to give players a chance to split messages.
            } catch (Exception ex)
            {
                Logger.Error(ex, $"Exception sending response. RadioId {0}, Response length {length}");
            }
            Thread.Sleep(1000);
        }
    }
}

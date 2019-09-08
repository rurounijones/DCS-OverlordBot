using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using FragLabs.Audio.Codecs;
using NLog;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class TCPVoiceHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static volatile RadioSendingState RadioSendingState = new RadioSendingState();
        public static volatile RadioReceivingState[] RadioReceivingState = new RadioReceivingState[11];

        private readonly IPAddress _address;
        private readonly AudioManager _audioManager;
        private readonly ConcurrentDictionary<string, SRClient> _clientsList;


        private readonly BlockingCollection<byte[]> _encodedAudio = new BlockingCollection<byte[]>();
        private readonly string _guid;
        private readonly byte[] _guidAsciiBytes;
        private readonly InputDeviceManager _inputManager;
        private readonly int _port;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();
        private readonly CancellationTokenSource _pingStop = new CancellationTokenSource();

        private readonly int JITTER_BUFFER = 50; //in milliseconds

        //    private readonly JitterBuffer _jitterBuffer = new JitterBuffer();
        private TcpClient _listener;

        private ulong _packetNumber = 1;

        private volatile bool _ptt;

        private volatile bool _stop;

        private volatile bool _ready;

        private Timer _timer;
        private bool hasSentVoicePacket; //used to force sending of first voice packet to establish comms

        private ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        private readonly SettingsStore _settings = SettingsStore.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        private readonly AudioManager.VOIPConnectCallback _voipConnectCallback;

        public TCPVoiceHandler(ConcurrentDictionary<string, SRClient> clientsList, string guid, IPAddress address,
            int port, OpusDecoder decoder, AudioManager audioManager, InputDeviceManager inputManager, AudioManager.VOIPConnectCallback voipConnectCallback)
        {
           // _decoder = decoder;
            _audioManager = audioManager;

            _clientsList = clientsList;
            _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

            _guid = guid;
            _address = address;
            _port = port + 1;

            _inputManager = inputManager;

            _voipConnectCallback = voipConnectCallback;
        }

        private void AudioEffectCheckTick()
        {
            for (var i = 0; i < RadioReceivingState.Length; i++)
            {
                //Nothing on this radio!
                //play out if nothing after 200ms
                //and Audio hasn't been played already
                var radioState = RadioReceivingState[i];
                if ((radioState != null) && !radioState.PlayedEndOfTransmission && !radioState.IsReceiving)
                {
                    radioState.PlayedEndOfTransmission = true;

                    var radioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

                    if (!radioState.IsSimultaneous)
                    {
                        _audioManager.PlaySoundEffectEndReceive(i, radioInfo.radios[i].volume);
                    }
                }
            }
        }

        public void Listen()
        {
            _ready = false;

            //start audio processing threads
            var decoderThread = new Thread(UdpAudioDecode);
            decoderThread.Start();

            var settings = SettingsStore.Instance;

            StartTimer();

            StartPing();

            //keep reconnecting until stop
            while (!_stop)
            {
                try
                {
                    //set to false so we sent one packet to open up the radio
                    //automatically rather than the user having to press Send
                    hasSentVoicePacket = false;

                    _packetNumber = 1; //reset packet number

                    _listener = new TcpClient();
                    _listener.NoDelay = true;

                    try
                    {
                        _listener.ConnectAsync(_address, _port).Wait(TimeSpan.FromSeconds(10));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed to connect to VOIP server @ {_address.ToString()}:{_port}");

                        CallOnMainVOIPConnect(false, true);

                        RequestStop();
                        break;
                    }

                    if (!_listener.Connected)
                    {
                        Logger.Error($"Failed to connect to VOIP server @ {_address.ToString()}:{_port}");

                        CallOnMainVOIPConnect(false, true);

                        RequestStop();
                        break;
                    }

                    //initial packet to get audio setup
                    var udpVoicePacket = new UDPVoicePacket
                    {
                        GuidBytes = _guidAsciiBytes,
                        AudioPart1Bytes = new byte[] {0, 1, 2, 3, 4, 5},
                        AudioPart1Length = (ushort) 6,
                        Frequencies = new double[] { 100 },
                        UnitId = 1,
                        Encryptions = new byte[] { 0 },
                        Modulations = new byte[] { 4 },
                        PacketNumber = 1
                    }.EncodePacket();

                    _listener.Client.Send(udpVoicePacket);

                    //contains short for audio packet length
                    byte[] lengthBuffer = new byte[2];

                    _ready = true;

                    Logger.Info("Connected to VOIP TCP " + _port);

                    CallOnMainVOIPConnect(true);

                    while (_listener.Connected && !_stop)
                    {
                        int received = _listener.Client.Receive(lengthBuffer, 2, SocketFlags.None);

                        if (received == 0)
                        {
                            // didnt receive enough, quit.
                            Logger.Warn(
                                "Didnt Receive full packet for VOIP - Disconnecting & Reconnecting if next Recieve fails");
                            //break;
                        }
                        else
                        {
                            ushort packetLength =
                                BitConverter.ToUInt16(new byte[2] {lengthBuffer[0], lengthBuffer[1]}, 0);

                            byte[] audioPacketBuffer = new byte[packetLength];

                            //add pack in length to full buffer for packet decode
                            audioPacketBuffer[0] = lengthBuffer[0];
                            audioPacketBuffer[1] = lengthBuffer[1];

                            received = _listener.Client.Receive(audioPacketBuffer, 2, packetLength - 2,
                                SocketFlags.None);

                            int offset = received + 2;
                            int remaining = packetLength - 2 - received;
                            while (remaining > 0 && received > 0)
                            {
                                received = _listener.Client.Receive(audioPacketBuffer, offset, remaining,
                                    SocketFlags.None);

                                remaining = remaining - received;
                                offset = offset + received;
                            }

                            if (remaining == 0)
                            {
                                _encodedAudio.Add(audioPacketBuffer);
                            }
                            else
                            {
                                //didnt receive enough - log and reconnect
                                Logger.Warn("Didnt Receive any packet for VOIP - Disconnecting & Reconnecting");
                                break;
                            }
                        }
                    }

                    _ready = false;
                }
                catch (Exception e)
                {
                    if (!_stop)
                    {
                        Logger.Error(e, "Error with VOIP TCP Connection on port " + _port + " Reconnecting");
                    }
                }

                try
                {
                    _listener.Close();
                }
                catch (Exception e)
                {
                }

                CallOnMainVOIPConnect(false);
            }
        }

        public void StartTimer()
        {
            StopTimer();

            // _jitterBuffer.Clear();
            _timer = new Timer(AudioEffectCheckTick, TimeSpan.FromMilliseconds(JITTER_BUFFER));
            _timer.Start();
        }

        public void StopTimer()
        {
            if (_timer != null)
            {
                //    _jitterBuffer.Clear();
                _timer.Stop();
                _timer = null;
            }
        }

        public void RequestStop()
        {
            _stop = true;
            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }

            _stopFlag.Cancel();
            _pingStop.Cancel();

            _inputManager.StopPtt();

            StopTimer();
        }

        private SRClient IsClientMetaDataValid(string clientGuid)
        {
            if (_clientsList.ContainsKey(clientGuid))
            {
                var client = _clientsList[_guid];

                if ((client != null) && client.isCurrent())
                {
                    return client;
                }
            }
            return null;
        }

        private void UdpAudioDecode()
        {
            try
            {
                while (!_stop)
                {
                    try
                    {
                        var encodedOpusAudio = new byte[0];
                        _encodedAudio.TryTake(out encodedOpusAudio, 100000, _stopFlag.Token);

                        var time = DateTime.Now.Ticks; //should add at the receive instead?

                        if ((encodedOpusAudio != null)
                            && (encodedOpusAudio.Length >= (UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength + UDPVoicePacket.FrequencySegmentLength)))
                        {
                            //  process
                            // check if we should play audio

                            var myClient = IsClientMetaDataValid(_guid);

                            if ((myClient != null) && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                            {
                                //Decode bytes
                                var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);
                                var frequencyCount = udpVoicePacket.Frequencies.Length;

                                List<RadioReceivingPriority> radioReceivingPriorities = new List<RadioReceivingPriority>(frequencyCount);
                                List<int> blockedRadios = CurrentlyBlockedRadios();

                                // Parse frequencies into receiving radio priority for selection below
                                for (var i = 0; i < frequencyCount; i++)
                                {
                                    RadioReceivingState state = null;
                                    bool decryptable;
                                    var radio = _clientStateSingleton.DcsPlayerRadioInfo.CanHearTransmission(
                                        udpVoicePacket.Frequencies[i],
                                        (RadioInformation.Modulation) udpVoicePacket.Modulations[i],
                                        udpVoicePacket.Encryptions[i],
                                        udpVoicePacket.UnitId,
                                        blockedRadios,
                                        out state, 
                                        out decryptable);

                                    float losLoss = 0.0f;
                                    double receivPowerLossPercent = 0.0;

                                    if (radio != null && state != null)
                                    {
                                        if (
                                            radio.modulation == RadioInformation.Modulation.INTERCOM
                                            || (
                                                HasLineOfSight(udpVoicePacket, out losLoss)
                                                && InRange(udpVoicePacket.Guid, udpVoicePacket.Frequencies[i], out receivPowerLossPercent)
                                                && !blockedRadios.Contains(state.ReceivedOn)
                                            )
                                        )
                                        {
                                            decryptable = (udpVoicePacket.Encryptions[i] == 0) || (udpVoicePacket.Encryptions[i] == radio.encKey && radio.enc);

                                            radioReceivingPriorities.Add(new RadioReceivingPriority()
                                            {
                                                Decryptable = decryptable,
                                                Encryption = udpVoicePacket.Encryptions[i],
                                                Frequency = udpVoicePacket.Frequencies[i],
                                                LineOfSightLoss = losLoss,
                                                Modulation = udpVoicePacket.Modulations[i],
                                                ReceivingPowerLossPercent = receivPowerLossPercent,
                                                ReceivingRadio = radio,
                                                ReceivingState = state
                                            });
                                        }
                                    }
                                }

                                // Sort receiving radios to play audio on correct one
                                radioReceivingPriorities.Sort(SortRadioReceivingPriorities);

                                if (radioReceivingPriorities.Count > 0)
                                {
                                        //ALL GOOD!
                                        //create marker for bytes
                                        for (int i = 0; i < radioReceivingPriorities.Count; i++)
                                        {
                                            var destinationRadio = radioReceivingPriorities[i];
                                            var isSimultaneousTransmission = radioReceivingPriorities.Count > 1 && i > 0;

                                            var audio = new ClientAudio
                                            {
                                                ClientGuid = udpVoicePacket.Guid,
                                                EncodedAudio = udpVoicePacket.AudioPart1Bytes,
                                                //Convert to Shorts!
                                                ReceiveTime = DateTime.Now.Ticks,
                                                Frequency = destinationRadio.Frequency,
                                                Modulation = destinationRadio.Modulation,
                                                Volume = destinationRadio.ReceivingRadio.volume,
                                                ReceivedRadio = destinationRadio.ReceivingState.ReceivedOn,
                                                UnitId = udpVoicePacket.UnitId,
                                                Encryption = destinationRadio.Encryption,
                                                Decryptable = destinationRadio.Decryptable,
                                                // mark if we can decrypt it
                                                RadioReceivingState = destinationRadio.ReceivingState,
                                                RecevingPower = destinationRadio.ReceivingPowerLossPercent, //loss of 1.0 or greater is total loss
                                                LineOfSightLoss = destinationRadio.LineOfSightLoss, // Loss of 1.0 or greater is total loss
                                                PacketNumber = udpVoicePacket.PacketNumber
                                            };

                                            RadioReceivingState[audio.ReceivedRadio] = new RadioReceivingState
                                            {
                                                IsSecondary = destinationRadio.ReceivingState.IsSecondary,
                                                IsSimultaneous = isSimultaneousTransmission,
                                                LastReceviedAt = DateTime.Now.Ticks,
                                                PlayedEndOfTransmission = false,
                                                ReceivedOn = destinationRadio.ReceivingState.ReceivedOn
                                            };

                                            // Only play actual audio once
                                            if (i == 0)
                                            {
                                                _audioManager.AddClientAudio(audio);
                                            }
                                        }
                                  

                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_stop)
                        {
                            Logger.Info(ex, "Failed to decode audio from Packet");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Stopped DeJitter Buffer");
            }
        }

        private List<int> CurrentlyBlockedRadios()
        {
            List<int> transmitting = new List<int>();
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX))
            {
                return transmitting;
            }

            if (!_ptt && !_clientStateSingleton.DcsPlayerRadioInfo.ptt)
            {
                return transmitting;
            }

            transmitting.Add(_clientStateSingleton.DcsPlayerRadioInfo.selected);

            if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
            {
                // Skip intercom
                for (int i = 1; i < 11; i++)
                {
                    var radio = _clientStateSingleton.DcsPlayerRadioInfo.radios[i];
                    if (radio.modulation != RadioInformation.Modulation.DISABLED && radio.simul && i != _clientStateSingleton.DcsPlayerRadioInfo.selected)
                    {
                        transmitting.Add(i);
                    }
                }
            }

            return transmitting;
        }

        private bool HasLineOfSight(UDPVoicePacket udpVoicePacket, out float losLoss)
        {
            losLoss = 0; //0 is NO LOSS
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED))
            {
                return true;
            }

            SRClient transmittingClient;
            if (_clientsList.TryGetValue(udpVoicePacket.Guid, out transmittingClient))
            {
                var myPosition = _clientStateSingleton.DcsPlayerRadioInfo.pos;

                var clientPos = transmittingClient.Position;

                if (((myPosition.x == 0) && (myPosition.z == 0)) || ((clientPos.x == 0) && (clientPos.z == 0)))
                {
                    //no real position therefore no line of Sight!
                    return true;
                }

                losLoss = transmittingClient.LineOfSightLoss;
                return transmittingClient.LineOfSightLoss < 1.0f; // 1.0 or greater  is TOTAL loss
            }

            losLoss = 0;
            return false;
        }

        private bool InRange(string transmissingClientGuid, double frequency, out double signalStrength)
        {
            signalStrength = 0;
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED))
            {
                return true;
            }

            SRClient transmittingClient;
            if (_clientsList.TryGetValue(transmissingClientGuid, out transmittingClient))
            {
                var myPosition = _clientStateSingleton.DcsPlayerRadioInfo.pos;

                var clientPos = transmittingClient.Position;

                if (((myPosition.x == 0) && (myPosition.z == 0)) || ((clientPos.x == 0) && (clientPos.z == 0)))
                {
                    //no real position
                    return true;
                }

                var dist = RadioCalculator.CalculateDistance(myPosition, clientPos);

                var max = RadioCalculator.FriisMaximumTransmissionRange(frequency);
                // % loss of signal
                // 0 is no loss 1.0 is full loss
                signalStrength = (dist / max);

                return max > dist;
            }
            return false;
        }

        private int SortRadioReceivingPriorities(RadioReceivingPriority x, RadioReceivingPriority y)
        {
            int xScore = 0;
            int yScore = 0;

            if (x.ReceivingRadio == null || x.ReceivingState == null)
            {
                return 1;
            }
            if (y.ReceivingRadio == null | y.ReceivingState == null)
            {
                return -1;
            }

            if (x.Decryptable)
            {
                xScore += 16;
            }
            if (y.Decryptable)
            {
                yScore += 16;
            }

            if (_clientStateSingleton.DcsPlayerRadioInfo.selected == x.ReceivingState.ReceivedOn)
            {
                xScore += 8;
            }
            if (_clientStateSingleton.DcsPlayerRadioInfo.selected == y.ReceivingState.ReceivedOn)
            {
                yScore += 8;
            }

            if (x.ReceivingRadio.volume > 0)
            {
                xScore += 4;
            }
            if (y.ReceivingRadio.volume > 0)
            {
                yScore += 4;
            }

            return yScore - xScore;
        }

        public bool Send(byte[] bytes, int len)
        {
            //if either PTT is true, a microphone is available && socket connected etc
            if (_ready
                && _listener != null
                && _listener.Connected
                && (_ptt || _clientStateSingleton.DcsPlayerRadioInfo.ptt)
                && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent()
                && _clientStateSingleton.MicrophoneAvailable
                && (bytes != null))
                //can only send if DCS is connected
            {
                try
                {
                    // List of radios the transmission is sent to (can me multiple if simultaneous transmission is enabled)
                    List<RadioInformation> transmittingRadios = new List<RadioInformation>();

                    // Always add currently selected radio (if valid)
                    var currentSelected = _clientStateSingleton.DcsPlayerRadioInfo.selected;
                    RadioInformation currentlySelectedRadio = null;
                    if (currentSelected >= 0 
                        && currentSelected < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length)
                    {
                        currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[currentSelected];
                        
                        if (currentlySelectedRadio != null && currentlySelectedRadio.modulation != RadioInformation.Modulation.DISABLED
                            && (currentlySelectedRadio.freq > 100 || currentlySelectedRadio.modulation == RadioInformation.Modulation.INTERCOM))
                        {
                            transmittingRadios.Add(currentlySelectedRadio);
                        }
                    }

                    // Add all radios toggled for simultaneous transmission if the global flag has been set
                    if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
                    {
                        foreach (var radio in _clientStateSingleton.DcsPlayerRadioInfo.radios)
                        {
                            if (radio != null && radio.simul && radio.modulation != RadioInformation.Modulation.DISABLED
                                && (radio.freq > 100 || radio.modulation == RadioInformation.Modulation.INTERCOM)
                                && !transmittingRadios.Contains(radio)) // Make sure we don't add the selected radio twice
                            {
                                transmittingRadios.Add(radio);
                            }
                        }
                    }

                    if (transmittingRadios.Count > 0)
                    {
                        List<double> frequencies = new List<double>(transmittingRadios.Count);
                        List<byte> encryptions = new List<byte>(transmittingRadios.Count);
                        List<byte> modulations = new List<byte>(transmittingRadios.Count);

                        for (int i = 0; i < transmittingRadios.Count; i++)
                        {
                            var radio = transmittingRadios[i];

                            // Further deduplicate transmitted frequencies if they have the same freq./modulation/encryption (caused by differently named radios)
                            bool alreadyIncluded = false;
                            for (int j = 0; j < frequencies.Count; j++)
                            {
                                if (frequencies[j] == radio.freq
                                    && modulations[j] == (byte)radio.modulation
                                    && encryptions[j] == (radio.enc ? radio.encKey : (byte)0))
                                {
                                    alreadyIncluded = true;
                                    break;
                                }
                            }

                            if (alreadyIncluded)
                            {
                                continue;
                            }

                            frequencies.Add(radio.freq);
                            encryptions.Add(radio.enc ? radio.encKey : (byte)0);
                            modulations.Add((byte)radio.modulation);
                        }

                        //generate packet
                        var udpVoicePacket = new UDPVoicePacket
                        {
                            GuidBytes = _guidAsciiBytes,
                            AudioPart1Bytes = bytes,
                            AudioPart1Length = (ushort)bytes.Length,
                            Frequencies = frequencies.ToArray(),
                            UnitId = _clientStateSingleton.DcsPlayerRadioInfo.unitId,
                            Encryptions = encryptions.ToArray(),
                            Modulations = modulations.ToArray(),
                            PacketNumber = _packetNumber++
                        };

                        var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                        //send audio
                        _listener.Client.Send(encodedUdpVoicePacket);

                        //not sending or really quickly switched sending
                        if (currentlySelectedRadio != null &&
                            (!RadioSendingState.IsSending || RadioSendingState.SendingOn != currentSelected))
                        {
                            _audioManager.PlaySoundEffectStartTransmit(currentSelected,
                                currentlySelectedRadio.enc && (currentlySelectedRadio.encKey > 0), currentlySelectedRadio.volume);
                        }

                        //set radio overlay state
                        RadioSendingState = new RadioSendingState
                        {
                            IsSending = true,
                            LastSentAt = DateTime.Now.Ticks,
                            SendingOn = currentSelected
                        };
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Sending Audio Message " + e.Message);
                }
            }
            else
            {
                if (RadioSendingState.IsSending)
                {
                    RadioSendingState.IsSending = false;

                    if (RadioSendingState.SendingOn >= 0)
                    {
                        var radio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioSendingState.SendingOn];

                        _audioManager.PlaySoundEffectEndTransmit(RadioSendingState.SendingOn, radio.volume);
                    }
                }
            }
            return false;
        }

        public bool Send(byte[] bytes, int len, int radioId)
        {
            try
            {
                // List of radios the transmission is sent to (can me multiple if simultaneous transmission is enabled)
                List<RadioInformation> transmittingRadios = new List<RadioInformation>();

                // Always add radio specified by the bot
                var currentSelected = radioId;
                RadioInformation currentlySelectedRadio = null;
                if (currentSelected >= 0
                    && currentSelected < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length)
                {
                    currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[currentSelected];

                    if (currentlySelectedRadio != null && currentlySelectedRadio.modulation != RadioInformation.Modulation.DISABLED
                        && (currentlySelectedRadio.freq > 100 || currentlySelectedRadio.modulation == RadioInformation.Modulation.INTERCOM))
                    {
                        transmittingRadios.Add(currentlySelectedRadio);
                    }
                }

                // Add all radios toggled for simultaneous transmission if the global flag has been set
                if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
                {
                    foreach (var radio in _clientStateSingleton.DcsPlayerRadioInfo.radios)
                    {
                        if (radio != null && radio.simul && radio.modulation != RadioInformation.Modulation.DISABLED
                            && (radio.freq > 100 || radio.modulation == RadioInformation.Modulation.INTERCOM)
                            && !transmittingRadios.Contains(radio)) // Make sure we don't add the selected radio twice
                        {
                            transmittingRadios.Add(radio);
                        }
                    }
                }

                List<double> frequencies = new List<double>(transmittingRadios.Count);
                List<byte> encryptions = new List<byte>(transmittingRadios.Count);
                List<byte> modulations = new List<byte>(transmittingRadios.Count);

                for (int i = 0; i < transmittingRadios.Count; i++)
                {
                    var radio = transmittingRadios[i];

                    // Further deduplicate transmitted frequencies if they have the same freq./modulation/encryption (caused by differently named radios)
                    bool alreadyIncluded = false;
                    for (int j = 0; j < frequencies.Count; j++)
                    {
                        if (frequencies[j] == radio.freq
                            && modulations[j] == (byte)radio.modulation
                            && encryptions[j] == (radio.enc ? radio.encKey : (byte)0))
                        {
                            alreadyIncluded = true;
                            break;
                        }
                    }

                    if (alreadyIncluded)
                    {
                        continue;
                    }

                    frequencies.Add(radio.freq);
                    encryptions.Add(radio.enc ? radio.encKey : (byte)0);
                    modulations.Add((byte)radio.modulation);
                }

                //generate packet
                var udpVoicePacket = new UDPVoicePacket
                {
                    GuidBytes = _guidAsciiBytes,
                    AudioPart1Bytes = bytes,
                    AudioPart1Length = (ushort)bytes.Length,
                    Frequencies = frequencies.ToArray(),
                    UnitId = _clientStateSingleton.DcsPlayerRadioInfo.unitId,
                    Encryptions = encryptions.ToArray(),
                    Modulations = modulations.ToArray(),
                    PacketNumber = _packetNumber++
                };

                var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                _listener.Client.Send(encodedUdpVoicePacket);

                //set radio overlay state
                RadioSendingState = new RadioSendingState
                {
                    IsSending = true,
                    LastSentAt = DateTime.Now.Ticks,
                    SendingOn = currentSelected
                };
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending Audio Message " + e.Message);
                return false;
            }
        }

        private void StartPing()
        {
            Logger.Info("Pinging Server - Starting");
            var thread = new Thread(() =>
            {
                byte[] message = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15};

                while (!_stop)
                {
                    //wait for cancel or quit    
                    var cancelled = _pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(60));

                    if (cancelled)
                    {
                        return;
                    }

                    try
                    {
                        if (!RadioSendingState.IsSending && _listener != null && _listener.Connected)
                        {
                            var udpVoicePacket = new UDPVoicePacket
                            {
                                GuidBytes = _guidAsciiBytes,
                                AudioPart1Bytes = message,
                                AudioPart1Length = (ushort) message.Length,
                                Frequencies = new double[] { 100 },
                                UnitId = 1,
                                Encryptions = new byte[] { 0 },
                                Modulations = new byte[] { 4 },
                                PacketNumber = 1
                            }.EncodePacket();

                            _listener.Client.Send(udpVoicePacket);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                    }
                }
            });
            thread.Start();
        }

        private void CallOnMainVOIPConnect(bool result, bool connectionError = false)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new ThreadStart(delegate { _voipConnectCallback(result, connectionError, $"{_address.ToString()}:{_port}"); }));
            }
            catch (Exception ex)
            {
            }
        }
    }
}
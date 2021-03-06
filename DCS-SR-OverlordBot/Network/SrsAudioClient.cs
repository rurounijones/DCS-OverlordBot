using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using RurouniJones.DCS.OverlordBot.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using NLog;
using static Ciribob.DCS.SimpleRadio.Standalone.Common.RadioInformation;
using Timer = Cabhishek.Timers.Timer;

namespace RurouniJones.DCS.OverlordBot.Network
{
    public class SrsAudioClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public RadioSendingState RadioSendingState = new RadioSendingState();
        public RadioReceivingState[] RadioReceivingState = new RadioReceivingState[11];

        private readonly BlockingCollection<byte[]> _encodedAudio = new BlockingCollection<byte[]>();
        private readonly string _guid;
        private readonly byte[] _guidAsciiBytes;
        private readonly CancellationTokenSource _pingStop = new CancellationTokenSource();
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();

        private const int JitterBuffer = 50; //in milliseconds

        private readonly Client _mainClient;

        //    private readonly JitterBuffer _jitterBuffer = new JitterBuffer();
        private UdpClient _listener;

        private ulong _packetNumber = 1;

        private readonly IPEndPoint _serverEndpoint;

        private volatile bool _requestStop;

        private Timer _transmissionEndCheckTimer;

        private long _udpLastReceived;
        private DispatcherTimer _udpTimeoutChecker;

        public SrsAudioClient(Client mainClient)
        {
            _guid = mainClient.ShortGuid;
            _guidAsciiBytes = Encoding.ASCII.GetBytes(_guid);

            _mainClient = mainClient;

            _serverEndpoint = mainClient.Endpoint;
        }

        private void CheckUdpTimeout(object sender, EventArgs e)
        {
            var diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

            //ping every 15 so after 35 seconds VoIP UDP issue
            if (diff.Seconds <= 35)
            {
                _mainClient.IsAudioConnected = true;
            }
            else
            {
                Logger.Info($"{_mainClient.LogClientId}| {diff.Seconds} seconds since last Received UDP data from Server. Stopping Audio Client");
                _mainClient.IsAudioConnected = false;
            }
        }

        private void CheckTransmissionEnded()
        {
            //Nothing on this radio!
            //play out if nothing after 200ms
            //and Audio hasn't been played already
            var radioState = RadioReceivingState[0];
            if (radioState == null || radioState.PlayedEndOfTransmission || radioState.IsReceiving) return;
            radioState.PlayedEndOfTransmission = true;
            _mainClient.AudioManager.EndTransmission();
        }

        public void Listen()
        {
            _requestStop = false;
            _udpLastReceived = 0;

            _udpTimeoutChecker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _udpTimeoutChecker.Tick += CheckUdpTimeout;
            _udpTimeoutChecker.Start();

            var decoderThread = new Thread(UdpAudioDecode) {Name = $"{_mainClient.LogClientId}|  Audio Decoder"};
            decoderThread.Start();

            StartTransmissionEndCheckTimer();

            _packetNumber = 1; //reset packet number

            var localListenEndpoint = new IPEndPoint(IPAddress.Any, 0); // 0 means random unused port
            
            Thread.Sleep(5000);

            while (!_requestStop)
            {
                try
                {
                    var bytes = _listener.Receive(ref localListenEndpoint);

                    if (bytes.Length == 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        Logger.Trace($"{_mainClient.LogClientId}| Received UDP Ping Back from Server");
                    }
                    else if (bytes.Length > 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        _encodedAudio.Add(bytes);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"{_mainClient.LogClientId}| Error Receiving UDP data from Server");
                }
            }

            Logger.Debug($"{_mainClient.LogClientId}| Stopping ListenLoop. RequestStop is {_requestStop}");
            _mainClient.IsAudioConnected = false;
        }

        public void StartTransmissionEndCheckTimer()
        {
            _transmissionEndCheckTimer = new Timer(CheckTransmissionEnded, TimeSpan.FromMilliseconds(JitterBuffer));
            _transmissionEndCheckTimer.Start();
        }

        public void RequestStop()
        {
            _requestStop = true;

            _transmissionEndCheckTimer?.Stop();
            _transmissionEndCheckTimer = null;

            _udpTimeoutChecker?.Stop();
            _udpTimeoutChecker = null;

            _stopFlag.Cancel();
            _pingStop.Cancel();

            _listener?.Dispose();
            _listener = null;
        }

        private SRClient IsClientMetaDataValid(string clientGuid)
        {
            if (!_mainClient.ContainsKey(clientGuid)) return null;
            var client = _mainClient[_guid];

            return client;
        }

        private void UdpAudioDecode()
        {
            try
            {
                while (!_requestStop)
                {
                    try
                    {
                        var encodedOpusAudio = new byte[0];

                        try
                        {
                            _encodedAudio.TryTake(out encodedOpusAudio, 100000, _stopFlag.Token);
                        } catch(OperationCanceledException ex)
                        {
                            Logger.Debug(ex, $"{_mainClient.LogClientId}| Cancelled operating to get encoded audio");
                        }

                        if (encodedOpusAudio == null ||
                            encodedOpusAudio.Length <
                            UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength +
                            UDPVoicePacket.FrequencySegmentLength) continue;
                        //  process
                        // check if we should play audio

                        var myClient = IsClientMetaDataValid(_guid);

                        if (myClient == null) continue;
                        //Decode bytes
                        var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);

                        if (udpVoicePacket == null || (Modulation) udpVoicePacket.Modulations[0] != Modulation.AM &&
                            (Modulation) udpVoicePacket.Modulations[0] != Modulation.FM)
                            continue;
                        var globalFrequencies = _serverSettings.GlobalFrequencies;

                        var frequencyCount = udpVoicePacket.Frequencies.Length;

                        var radioReceivingPriorities =
                            new List<RadioReceivingPriority>(frequencyCount);
                        var blockedRadios = CurrentlyBlockedRadios();

                        // Parse frequencies into receiving radio priority for selection below
                        for (var i = 0; i < frequencyCount; i++)
                        {
                            //Check if Global
                            var globalFrequency = globalFrequencies.Contains(udpVoicePacket.Frequencies[i]);

                            if (globalFrequency)
                            {
                                //remove encryption for global
                                udpVoicePacket.Encryptions[i] = 0;
                            }

                            var radio = _mainClient.DcsPlayerRadioInfo.CanHearTransmission(
                                udpVoicePacket.Frequencies[i],
                                (Modulation) udpVoicePacket.Modulations[i],
                                udpVoicePacket.Encryptions[i],
                                udpVoicePacket.UnitId,
                                blockedRadios,
                                out var state,
                                out var decryptable);

                            var losLoss = 0.0f;
                            var receivPowerLossPercent = 0.0;

                            if (radio == null || state == null) continue;
                            if (radio.modulation != Modulation.INTERCOM && !globalFrequency &&
                                (!HasLineOfSight(udpVoicePacket, out losLoss) || !InRange(udpVoicePacket.Guid,
                                    udpVoicePacket.Frequencies[i],
                                    out receivPowerLossPercent) || blockedRadios.Contains(state.ReceivedOn))) continue;
                            decryptable =
                                udpVoicePacket.Encryptions[i] == 0 ||
                                udpVoicePacket.Encryptions[i] == radio.encKey && radio.enc;

                            radioReceivingPriorities.Add(new RadioReceivingPriority
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

                        // Sort receiving radios to play audio on correct one
                        radioReceivingPriorities.Sort(SortRadioReceivingPriorities);

                        if (radioReceivingPriorities.Count <= 0) continue;
                        {
                            //ALL GOOD!
                            //create marker for bytes
                            for (var i = 0; i < radioReceivingPriorities.Count; i++)
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
                                    RecevingPower =
                                        destinationRadio
                                            .ReceivingPowerLossPercent, //loss of 1.0 or greater is total loss
                                    LineOfSightLoss =
                                        destinationRadio
                                            .LineOfSightLoss, // Loss of 1.0 or greater is total loss
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
                                    _mainClient.AudioManager.AddClientAudio(audio);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_requestStop)
                        {
                            Logger.Info(ex, $"{_mainClient.LogClientId}| Failed to decode audio from Packet");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"{_mainClient.LogClientId}| Stopped DeJitter Buffer");
            }
            _mainClient.IsAudioConnected = false;
        }

        private List<int> CurrentlyBlockedRadios()
        {
            var transmitting = new List<int>();
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX))
            {
                return transmitting;
            }

            transmitting.Add(_mainClient.DcsPlayerRadioInfo.selected);

            if (!_mainClient.DcsPlayerRadioInfo.simultaneousTransmission) return transmitting;
            // Skip intercom
            for (var i = 1; i < 11; i++)
            {
                var radio = _mainClient.DcsPlayerRadioInfo.radios[i];
                if (radio.modulation != Modulation.DISABLED && radio.simul &&
                    i != _mainClient.DcsPlayerRadioInfo.selected)
                {
                    transmitting.Add(i);
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

            if (_mainClient.TryGetValue(udpVoicePacket.Guid, out var transmittingClient))
            {
                var myLatLng= _mainClient.PlayerCoalitionLocationMetadata.LngLngPosition;
                var clientLatLng = transmittingClient.LatLngPosition;
                if (myLatLng == null || clientLatLng == null || !myLatLng.isValid() || !clientLatLng.isValid())
                {
                    return true;
                }
                
                losLoss = transmittingClient.LineOfSightLoss;
                return transmittingClient.LineOfSightLoss < 1.0f; // 1.0 or greater  is TOTAL loss
                
            }

            losLoss = 0;
            return false;
        }

        private bool InRange(string transmittingClientGuid, double frequency, out double signalStrength)
        {
            signalStrength = 0;
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED))
            {
                return true;
            }

            if (!_mainClient.TryGetValue(transmittingClientGuid, out var transmittingClient)) return false;
            var myLatLng = _mainClient.PlayerCoalitionLocationMetadata.LngLngPosition;
            var clientLatLng = transmittingClient.LatLngPosition;
            //No DCS Position - do we have LotATC Position?
            if (myLatLng == null || clientLatLng == null || !myLatLng.isValid() || !clientLatLng.isValid())
            {
                return true;
            }

            //Calculate with Haversine (distance over ground) + Pythagoras (crow flies distance)
            var dist = RadioCalculator.CalculateDistanceHaversine(myLatLng, clientLatLng);

            var max = RadioCalculator.FriisMaximumTransmissionRange(frequency);
            // % loss of signal
            // 0 is no loss 1.0 is full loss
            signalStrength = dist / max;

            return max > dist;

        }

        private int SortRadioReceivingPriorities(RadioReceivingPriority x, RadioReceivingPriority y)
        {
            var xScore = 0;
            var yScore = 0;

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

            if (_mainClient.DcsPlayerRadioInfo.selected == x.ReceivingState.ReceivedOn)
            {
                xScore += 8;
            }

            if (_mainClient.DcsPlayerRadioInfo.selected == y.ReceivingState.ReceivedOn)
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

        public bool Send(byte[] bytes, int radioId)
        {
            if (bytes != null)
            {
                try
                {
                    var currentlySelectedRadio = _mainClient.DcsPlayerRadioInfo.radios[radioId];

                    var frequencies = new List<double>(1);
                    var encryptions = new List<byte>(1);
                    var modulations = new List<byte>(1);

                    frequencies.Add(currentlySelectedRadio.freq);
                    encryptions.Add(currentlySelectedRadio.enc ? currentlySelectedRadio.encKey : (byte)0);
                    modulations.Add((byte)currentlySelectedRadio.modulation);

                    //generate packet
                    var udpVoicePacket = new UDPVoicePacket
                    {
                        GuidBytes = _guidAsciiBytes,
                        OriginalClientGuidBytes = _guidAsciiBytes,
                        AudioPart1Bytes = bytes,
                        AudioPart1Length = (ushort)bytes.Length,
                        Frequencies = frequencies.ToArray(),
                        UnitId = _mainClient.DcsPlayerRadioInfo.unitId,
                        Encryptions = encryptions.ToArray(),
                        Modulations = modulations.ToArray(),
                        PacketNumber = _packetNumber++
                    };

                    var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                    _listener.Send(encodedUdpVoicePacket, encodedUdpVoicePacket.Length, _serverEndpoint);

                    RadioSendingState = new RadioSendingState
                    {
                        IsSending = true,
                        LastSentAt = DateTime.Now.Ticks,
                        SendingOn = radioId
                    };
                    return true;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"{_mainClient.LogClientId}| Exception Sending Audio Message " + e.Message);
                    return false;
                }
            }

            if (RadioSendingState.IsSending)
            {
                RadioSendingState.IsSending = false;
            }
            return false;
        }

        public void StartPing()
        {
            Logger.Debug($"{_mainClient.LogClientId}| Pinging Server - Starting");

            var message = _guidAsciiBytes;
            
            _listener = new UdpClient();
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch { }

            // Force immediate ping once to avoid race condition before starting to listen
            _listener.Send(message, message.Length, _serverEndpoint);
            _mainClient.IsAudioConnected = true;

            //wait for initial sync - then ping
            if (_pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
            {
                return;
            }

            while (!_requestStop)
            {
                //Logger.Info("Pinging Server");
                try
                {
                    if (!RadioSendingState.IsSending)
                    {
                        Logger.Trace($"{_mainClient.LogClientId}| Sending UDP Ping to server {_serverEndpoint}: {_guid}");
                        _listener?.Send(message, message.Length, _serverEndpoint);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"{_mainClient.LogClientId}| Exception Sending UDP Ping! " + e.Message);
                }

                if ( _pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15)))
                {
                    return;
                }
            }

            Logger.Debug($"{_mainClient.LogClientId}| Stopping PingLoop because RequestStop is {_requestStop}");
        }
    }
}
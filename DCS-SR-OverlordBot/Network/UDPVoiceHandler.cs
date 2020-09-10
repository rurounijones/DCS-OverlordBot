using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using NLog;
using static Ciribob.DCS.SimpleRadio.Standalone.Common.RadioInformation;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class UdpVoiceHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static volatile RadioSendingState RadioSendingState = new RadioSendingState();
        public static volatile RadioReceivingState[] RadioReceivingState = new RadioReceivingState[11];

        private readonly AudioManager _audioManager;

        private readonly BlockingCollection<byte[]> _encodedAudio = new BlockingCollection<byte[]>();
        private readonly string _guid;
        private readonly byte[] _guidAsciiBytes;
        private readonly CancellationTokenSource _pingStop = new CancellationTokenSource();
        private readonly int _port;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();

        private const int _jitterBuffer = 50; //in milliseconds

        private readonly Client _clientState;

        //    private readonly JitterBuffer _jitterBuffer = new JitterBuffer();
        private UdpClient _listener;

        private ulong _packetNumber = 1;

        private readonly IPEndPoint _serverEndpoint;

        private volatile bool _stop;

        private Timer _timer;

        private long _udpLastReceived;
        private readonly DispatcherTimer _updateTimer;

        public UdpVoiceHandler(string guid, IPEndPoint endpoint, AudioManager audioManager, Client client)
        {
            _audioManager = audioManager;
            _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

            _guid = guid;
            _port = endpoint.Port;

            _clientState = client;

            _serverEndpoint = endpoint;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _updateTimer.Tick += UpdateVoipStatus;
            _updateTimer.Start();
        }

        private void UpdateVoipStatus(object sender, EventArgs e)
        {
            var diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

            //ping every 15 so after 35 seconds VoIP UDP issue
            if (diff.Seconds <= 35)
            {
                _clientState.IsConnected = true;
            }
            else
            {
                Logger.Info($"{diff.Seconds} seconds since last Received UDP data from Server");
                _clientState.IsConnected = false;
            }
        }

        private void CheckTransmissionEnded()
        {
            for (var i = 0; i < RadioReceivingState.Length; i++)
            {
                //Nothing on this radio!
                //play out if nothing after 200ms
                //and Audio hasn't been played already
                var radioState = RadioReceivingState[i];
                if (radioState == null || radioState.PlayedEndOfTransmission || radioState.IsReceiving) continue;
                radioState.PlayedEndOfTransmission = true; 
                _audioManager.EndTransmission(i);
            }
        }

        public void Listen()
        {
            _udpLastReceived = 0;
            _listener = new UdpClient();
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch { }

            var decoderThread = new Thread(UdpAudioDecode) {Name = "Audio Decoder"};
            decoderThread.Start();

            StartTimer();

            StartPing();

            _packetNumber = 1; //reset packet number

            var localListenEndpoint = new IPEndPoint(IPAddress.Any, 0); // 0 means random unused port

            while (!_stop)
            {
                try
                {
                    var bytes = _listener.Receive(ref localListenEndpoint);

                    if (bytes.Length == 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        Logger.Debug("Received UDP Ping Back from Server");
                    }
                    else if (bytes.Length > 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        _encodedAudio.Add(bytes);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Error Receiving UDP data from Server");
                }
            }

            //stop UI Refreshing
            _updateTimer.Stop();

            _clientState.IsConnected = false;
        }

        public void StartTimer()
        {
            StopTimer();

            // _jitterBuffer.Clear();
            _timer = new Timer(CheckTransmissionEnded, TimeSpan.FromMilliseconds(_jitterBuffer));
            _timer.Start();
        }

        public void StopTimer()
        {
            if (_timer == null) return;
            //    _jitterBuffer.Clear();
            _timer.Stop();
            _timer = null;
        }

        public void RequestStop()
        {
            _stop = true;
            try
            {
                _listener.Close();
            }
            catch (Exception)
            {
            }

            _stopFlag.Cancel();
            _pingStop.Cancel();

            StopTimer();
        }

        private SRClient IsClientMetaDataValid(string clientGuid)
        {
            if (!_clientState.ContainsKey(clientGuid)) return null;
            var client = _clientState[_guid];

            return client;
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

                        try
                        {
                            _encodedAudio.TryTake(out encodedOpusAudio, 100000, _stopFlag.Token);
                        } catch(OperationCanceledException ex)
                        {
                            Logger.Debug(ex, "Cancelled operating to get encoded audio");
                        }

                        if (encodedOpusAudio == null ||
                            encodedOpusAudio.Length <
                            UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength +
                            UDPVoicePacket.FrequencySegmentLength) continue;
                        //  process
                        // check if we should play audio

                        var myClient = IsClientMetaDataValid(_guid);

                        if (myClient == null || !_clientState.DcsPlayerRadioInfo.IsCurrent()) continue;
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

                            var radio = _clientState.DcsPlayerRadioInfo.CanHearTransmission(
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
                                    _audioManager.AddClientAudio(audio);
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
            var transmitting = new List<int>();
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX))
            {
                return transmitting;
            }

            transmitting.Add(_clientState.DcsPlayerRadioInfo.selected);

            if (!_clientState.DcsPlayerRadioInfo.simultaneousTransmission) return transmitting;
            // Skip intercom
            for (var i = 1; i < 11; i++)
            {
                var radio = _clientState.DcsPlayerRadioInfo.radios[i];
                if (radio.modulation != Modulation.DISABLED && radio.simul &&
                    i != _clientState.DcsPlayerRadioInfo.selected)
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

            if (_clientState.TryGetValue(udpVoicePacket.Guid, out var transmittingClient))
            {
                var myLatLng= _clientState.PlayerCoalitionLocationMetadata.LngLngPosition;
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

            if (!_clientState.TryGetValue(transmittingClientGuid, out var transmittingClient)) return false;
            var myLatLng = _clientState.PlayerCoalitionLocationMetadata.LngLngPosition;
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

            if (_clientState.DcsPlayerRadioInfo.selected == x.ReceivingState.ReceivedOn)
            {
                xScore += 8;
            }

            if (_clientState.DcsPlayerRadioInfo.selected == y.ReceivingState.ReceivedOn)
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

        public bool Send(byte[] bytes, int len, int radioId)
        {
            if (bytes != null)
            {
                try
                {
                    var currentlySelectedRadio = _clientState.DcsPlayerRadioInfo.radios[radioId];

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
                        UnitId = _clientState.DcsPlayerRadioInfo.unitId,
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
                    Logger.Error(e, "Exception Sending Audio Message " + e.Message);
                    return false;
                }
            }

            if (RadioSendingState.IsSending)
            {
                RadioSendingState.IsSending = false;
            }
            return false;
        }

        private void StartPing()
        {
            Logger.Info("Pinging Server - Starting");

            var message = _guidAsciiBytes;

            // Force immediate ping once to avoid race condition before starting to listen
            _listener.Send(message, message.Length, _serverEndpoint);

            var thread = new Thread(() =>
            {
                //wait for initial sync - then ping
                if (_pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
                {
                    return;
                }

                while (!_stop)
                {
                    //Logger.Info("Pinging Server");
                    try
                    {
                        if (!RadioSendingState.IsSending)
                        {
                            Logger.Debug($"Sending UDP Ping to server {_serverEndpoint}: {_guid}");
                            _listener?.Send(message, message.Length, _serverEndpoint);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending UDP Ping! " + e.Message);
                    }

                    //wait for cancel or quit
                    var cancelled = _pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));

                    if (cancelled)
                    {
                        Logger.Debug($"Stopping UDP Server Ping to {_serverEndpoint} due to cancellation");
                        return;
                    }
                }

                Logger.Debug($"Stopping UDP Server Ping to {_serverEndpoint} due to leaving thread");
            }) {Name = "UDP Ping Sender"};
            thread.Start();
        }
    }
}
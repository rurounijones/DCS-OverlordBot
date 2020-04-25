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
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using FragLabs.Audio.Codecs;
using NLog;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    internal class UdpVoiceHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static volatile RadioSendingState RadioSendingState = new RadioSendingState();
        public static volatile RadioReceivingState[] RadioReceivingState = new RadioReceivingState[11];

        private readonly IPAddress _address;
        private readonly AudioManager _audioManager;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
        private readonly AudioInputSingleton _audioInputSingleton = AudioInputSingleton.Instance;

        private readonly BlockingCollection<byte[]> _encodedAudio = new BlockingCollection<byte[]>();
        private readonly string _guid;
        private readonly byte[] _guidAsciiBytes;
        private readonly InputDeviceManager _inputManager;
        private readonly CancellationTokenSource _pingStop = new CancellationTokenSource();
        private readonly int _port;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();


        private readonly int JITTER_BUFFER = 50; //in milliseconds

        private ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        //    private readonly JitterBuffer _jitterBuffer = new JitterBuffer();
        private UdpClient _listener;

        private ulong _packetNumber = 1;

        private volatile bool _ptt;

        private volatile bool _ready;

        private IPEndPoint _serverEndpoint;

        private volatile bool _stop;

        private Timer _timer;

        private long _udpLastReceived = 0;
        private DispatcherTimer _updateTimer;

        public UdpVoiceHandler(string guid, IPAddress address, int port, OpusDecoder decoder, AudioManager audioManager,
            InputDeviceManager inputManager)
        {
            // _decoder = decoder;
            _audioManager = audioManager;
            _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

            _guid = guid;
            _address = address;
            _port = port;

            _serverEndpoint = new IPEndPoint(_address, _port);

            _inputManager = inputManager;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _updateTimer.Tick += UpdateVOIPStatus;
            _updateTimer.Start();
        }

        private void UpdateVOIPStatus(object sender, EventArgs e)
        {
            TimeSpan diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

            //ping every 15 so after 35 seconds VoIP UDP issue
            if (diff.Seconds > 35)
            {
                _clientStateSingleton.IsVoipConnected = false;
            }
            else
            {
                _clientStateSingleton.IsVoipConnected = true;
            }
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
            _udpLastReceived = 0;
            _ready = false;
            _listener = new UdpClient();
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch { }
            // _listener.Connect(_serverEndpoint);

            //start 2 audio processing threads
            var decoderThread = new Thread(UdpAudioDecode);
            decoderThread.Start();

            var settings = GlobalSettingsStore.Instance;
            _inputManager.StartDetectPtt(pressed =>
            {
                var radios = _clientStateSingleton.DcsPlayerRadioInfo;

                var radioSwitchPtt = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);
                var radioSwitchPttWhenValid = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTTOnlyWhenValid);

                var ptt = false;
                foreach (var inputBindState in pressed)
                {
                    if (inputBindState.IsActive)
                    {
                        //radio switch?
                        if ((int) inputBindState.MainDevice.InputBind >= (int) InputBinding.Intercom &&
                            (int) inputBindState.MainDevice.InputBind <= (int) InputBinding.Switch10)
                        {
                            //gives you radio id if you minus 100
                            var radioId = (int) inputBindState.MainDevice.InputBind - 100;

                            if (radioId < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length)
                            {
                                var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[radioId];

                                if (clientRadio.modulation != RadioInformation.Modulation.DISABLED &&
                                    radios.control == DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
                                {
                                    radios.selected = (short) radioId;
                                    
                                    //turn on PTT
                                    if (radioSwitchPttWhenValid || radioSwitchPtt)
                                    {
                                        ptt = true;
                                    }
                                }
                                else
                                {
                                    //turn on PTT even if not valid radio switch
                                    if (radioSwitchPtt)
                                    {
                                        ptt = true;
                                    }
                                }

                            }
                        }
                        else if (inputBindState.MainDevice.InputBind == InputBinding.Ptt)
                        {
                            ptt = true;
                        }
                    }
                }

                //if length is zero - no keybinds or no PTT pressed set to false
                _ptt = ptt;
            });

            StartTimer();

            StartPing();

            _packetNumber = 1; //reset packet number

            while (!_stop)
            {
                _ready = true;
                try
                {
                    var groupEp = new IPEndPoint(IPAddress.Any, _port);
                    //   listener.Client.ReceiveTimeout = 3000;

                    var bytes = _listener.Receive(ref groupEp);

                    if (bytes?.Length == 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        Logger.Info("Received Ping Back from Server");
                    }
                    else if (bytes?.Length > 22)
                    {
                        _udpLastReceived = DateTime.Now.Ticks;
                        _encodedAudio.Add(bytes);
                    }
                }
                catch (Exception e)
                {
                    //  logger.Error(e, "error listening for UDP Voip");
                }
            }

            _ready = false;

            //stop UI Refreshing
            _updateTimer.Stop();

            _clientStateSingleton.IsVoipConnected = false;
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
            if (_clients.ContainsKey(clientGuid))
            {
                var client = _clients[_guid];

                if (client != null)
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
                            && (encodedOpusAudio.Length >=
                                (UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength +
                                 UDPVoicePacket.FrequencySegmentLength)))
                        {
                            //  process
                            // check if we should play audio

                            var myClient = IsClientMetaDataValid(_guid);

                            if ((myClient != null) && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                            {
                                //Decode bytes
                                var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);

                                if (udpVoicePacket != null && udpVoicePacket.Modulations[0] != 4)
                                {
                                    var globalFrequencies = _serverSettings.GlobalFrequencies;

                                    var frequencyCount = udpVoicePacket.Frequencies.Length;

                                    List<RadioReceivingPriority> radioReceivingPriorities =
                                        new List<RadioReceivingPriority>(frequencyCount);
                                    List<int> blockedRadios = CurrentlyBlockedRadios();

                                    // Parse frequencies into receiving radio priority for selection below
                                    for (var i = 0; i < frequencyCount; i++)
                                    {
                                        RadioReceivingState state = null;
                                        bool decryptable;

                                        //Check if Global
                                        bool globalFrequency = globalFrequencies.Contains(udpVoicePacket.Frequencies[i]);

                                        if (globalFrequency)
                                        {
                                            //remove encryption for global
                                            udpVoicePacket.Encryptions[i] = 0;
                                        }

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
                                                || globalFrequency
                                                || (
                                                    HasLineOfSight(udpVoicePacket, out losLoss)
                                                    && InRange(udpVoicePacket.Guid, udpVoicePacket.Frequencies[i],
                                                        out receivPowerLossPercent)
                                                    && !blockedRadios.Contains(state.ReceivedOn)
                                                )
                                            )
                                            {
                                                decryptable =
                                                    (udpVoicePacket.Encryptions[i] == 0) ||
                                                    (udpVoicePacket.Encryptions[i] == radio.encKey && radio.enc);

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
                                                RecevingPower =
                                                    destinationRadio
                                                        .ReceivingPowerLossPercent, //loss of 1.0 or greater is total loss
                                                LineOfSightLoss =
                                                    destinationRadio
                                                        .LineOfSightLoss, // Loss of 1.0 or greater is total loss
                                                PacketNumber = udpVoicePacket.PacketNumber
                                            };


                                            //handle effects
                                            var radioState = RadioReceivingState[audio.ReceivedRadio];

                                            if (!isSimultaneousTransmission &&
                                                (radioState == null || radioState.PlayedEndOfTransmission ||
                                                 !radioState.IsReceiving))
                                            {
                                                var audioDecryptable = audio.Decryptable || (audio.Encryption == 0);

                                                //mark that we have decrpyted encrypted audio for sound effects
                                                if (audioDecryptable && (audio.Encryption > 0))
                                                {
                                                    _audioManager.PlaySoundEffectStartReceive(audio.ReceivedRadio,
                                                        true,
                                                        audio.Volume);
                                                }
                                                else
                                                {
                                                    _audioManager.PlaySoundEffectStartReceive(audio.ReceivedRadio,
                                                        false,
                                                        audio.Volume);
                                                }
                                            }

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
                    if (radio.modulation != RadioInformation.Modulation.DISABLED && radio.simul &&
                        i != _clientStateSingleton.DcsPlayerRadioInfo.selected)
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
            if (_clients.TryGetValue(udpVoicePacket.Guid, out transmittingClient))
            {
                var myLatLng= _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition;
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

        private bool InRange(string transmissingClientGuid, double frequency, out double signalStrength)
        {
            signalStrength = 0;
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED))
            {
                return true;
            }

            SRClient transmittingClient;
            if (_clients.TryGetValue(transmissingClientGuid, out transmittingClient))
            {
                double dist = 0;
               
                var myLatLng = _clientStateSingleton.PlayerCoaltionLocationMetadata.LngLngPosition;
                var clientLatLng = transmittingClient.LatLngPosition;
                //No DCS Position - do we have LotATC Position?
                if (myLatLng == null || clientLatLng == null || !myLatLng.isValid() || !clientLatLng.isValid())
                {
                    return true;
                }
                else
                {
                    //Calculate with Haversine (distance over ground) + Pythagoras (crow flies distance)
                    dist = RadioCalculator.CalculateDistanceHaversine(myLatLng, clientLatLng);
                }

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

        private List<RadioInformation> PTTPressed(out int sendingOn)
        {
            sendingOn = -1;
            if (_clientStateSingleton.InhibitTX.InhibitTX)
            {
                TimeSpan time = new TimeSpan(DateTime.Now.Ticks - _clientStateSingleton.InhibitTX.LastReceivedAt);

                //inhibit for up to 5 seconds since the last message from VAICOM
                if (time.TotalSeconds < 5)
                {
                    return new List<RadioInformation>();
                }
            }

            var radioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
            //If its a hot intercom and thats not the currently selected radio
            //this is special logic currently for the gazelle as it has a hot mic, but no way of knowing if you're transmitting from the module itself
            //so we have to figure out what you're transmitting on in SRS
            if (radioInfo.intercomHotMic 
                && radioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT
                && radioInfo.selected != 0 && !_ptt && !radioInfo.ptt)
            {
                if (radioInfo.radios[0].modulation == RadioInformation.Modulation.INTERCOM)
                {
                    var intercom = new List<RadioInformation>();
                    intercom.Add(radioInfo.radios[0]);
                    sendingOn = 0;
                    return intercom;
                }
            }

            var transmittingRadios = new List<RadioInformation>();
            if (_ptt || _clientStateSingleton.DcsPlayerRadioInfo.ptt)
            {
                // Always add currently selected radio (if valid)
                var currentSelected = _clientStateSingleton.DcsPlayerRadioInfo.selected;
                RadioInformation currentlySelectedRadio = null;
                if (currentSelected >= 0
                    && currentSelected < _clientStateSingleton.DcsPlayerRadioInfo.radios.Length)
                {
                    currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[currentSelected];

                    if (currentlySelectedRadio != null && currentlySelectedRadio.modulation !=
                                                       RadioInformation.Modulation.DISABLED
                                                       && (currentlySelectedRadio.freq > 100 ||
                                                           currentlySelectedRadio.modulation ==
                                                           RadioInformation.Modulation.INTERCOM))
                    {
                        sendingOn = currentSelected;
                        transmittingRadios.Add(currentlySelectedRadio);
                    }
                }

                // Add all radios toggled for simultaneous transmission if the global flag has been set
                if (_clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission)
                {
                    var i = 0;
                    foreach (var radio in _clientStateSingleton.DcsPlayerRadioInfo.radios)
                    {
                        if (radio != null && radio.simul && radio.modulation != RadioInformation.Modulation.DISABLED
                            && (radio.freq > 100 || radio.modulation == RadioInformation.Modulation.INTERCOM)
                            && !transmittingRadios.Contains(radio)
                        ) // Make sure we don't add the selected radio twice
                        {
                            if (sendingOn == -1)
                            {
                                sendingOn = i;
                            }
                            transmittingRadios.Add(radio);
                        }

                        i++;
                    }
                }
            }

            return transmittingRadios;
        }

        public bool Send(byte[] bytes, int len)
        {
            // List of radios the transmission is sent to (can me multiple if simultaneous transmission is enabled)
            List<RadioInformation> transmittingRadios;
            //if either PTT is true, a microphone is available && socket connected etc
            var sendingOn = -1;
            if (_ready
                && _listener != null
                && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent()
                && _audioInputSingleton.MicrophoneAvailable
                && (bytes != null)
                && (transmittingRadios = PTTPressed(out sendingOn)).Count >0 )
                //can only send if DCS is connected
            {
                try
                {

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
                                    && modulations[j] == (byte) radio.modulation
                                    && encryptions[j] == (radio.enc ? radio.encKey : (byte) 0))
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
                            encryptions.Add(radio.enc ? radio.encKey : (byte) 0);
                            modulations.Add((byte) radio.modulation);
                        }

                        //generate packet
                        var udpVoicePacket = new UDPVoicePacket
                        {
                            GuidBytes = _guidAsciiBytes,
                            AudioPart1Bytes = bytes,
                            AudioPart1Length = (ushort) bytes.Length,
                            Frequencies = frequencies.ToArray(),
                            UnitId = _clientStateSingleton.DcsPlayerRadioInfo.unitId,
                            Encryptions = encryptions.ToArray(),
                            Modulations = modulations.ToArray(),
                            PacketNumber = _packetNumber++
                        };

                        var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                        _listener.Send(encodedUdpVoicePacket, encodedUdpVoicePacket.Length, new IPEndPoint(_address, _port));

                        var currentlySelectedRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[sendingOn];

                        //not sending or really quickly switched sending
                        if (currentlySelectedRadio != null &&
                            (!RadioSendingState.IsSending || RadioSendingState.SendingOn != sendingOn))
                        {
                            _audioManager.PlaySoundEffectStartTransmit(sendingOn,
                                currentlySelectedRadio.enc && (currentlySelectedRadio.encKey > 0),
                                currentlySelectedRadio.volume);
                        }

                        //set radio overlay state
                        RadioSendingState = new RadioSendingState
                        {
                            IsSending = true,
                            LastSentAt = DateTime.Now.Ticks,
                            SendingOn = sendingOn
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

        private void StartPing()
        {
            Logger.Info("Pinging Server - Starting");

            byte[] message = _guidAsciiBytes;

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
                        if (!RadioSendingState.IsSending && _listener != null)
                        {
                            _listener.Send(message, message.Length,_serverEndpoint);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                    }

                    //wait for cancel or quit
                    var cancelled = _pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));

                    if (cancelled)
                    {
                        return;
                    }
                }
            });
            thread.Start();
        }
    }
}
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public sealed class Client
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public readonly SrsClientSyncHandler SrsClientSyncHandler;
        private readonly AudioManager _audioManager;

        public DCSPlayerRadioInfo DcsPlayerRadioInfo { get; set; }
        public DCSPlayerSideInfo PlayerCoalitionLocationMetadata { get; set; }

        private IPEndPoint _endpoint;

        public long LastSent { get; set; }

        public bool IsTcpConnected { get; set; }

        public  UdpVoiceHandler UdpVoiceHandler;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if(value && !_isConnected )
                {
                    _logger.Debug($"Connection State {SrsClientSyncHandler.ConnectionState.Connected}");
                    SrsClientSyncHandler.ProcessConnectionState(SrsClientSyncHandler.ConnectionState.Connected);
                }
                if (!value == _isConnected)
                {
                    _logger.Debug($"Connection State {SrsClientSyncHandler.ConnectionState.Disconnected}");
                    SrsClientSyncHandler.ProcessConnectionState(SrsClientSyncHandler.ConnectionState.Disconnected);
                }
                _isConnected = value;

            }
        }

        public string ShortGuid { get; }

        // Indicates the user's desire to be in External Awacs Mode or not
        public bool ExternalAwacsModeSelected { get; set; }

        // Indicates whether we are *actually* connected in External Awacs Mode
        // Used by the Name and Password related UI elements to determine if they are editable or not
        public bool ExternalAwacsModeConnected { get; set; }

        public string LastSeenName { get; set; }

        public string ExternalAwacsModePassword { get; set; } = "hoggit1fw";

        public Client(AudioManager audioManager, DCSPlayerRadioInfo playerRadioInfo )
        {
            _audioManager = audioManager;
            ShortGuid = Common.Network.ShortGuid.NewGuid();
            DcsPlayerRadioInfo = playerRadioInfo;
            PlayerCoalitionLocationMetadata = new DCSPlayerSideInfo();
            SrsClientSyncHandler = new SrsClientSyncHandler(this);

            LastSent = 0;

            IsTcpConnected = false;
            ExternalAwacsModeSelected = false;

            LastSeenName = playerRadioInfo.name;
        }

        public void ConnectData(IPEndPoint endpoint)
        {
            _endpoint = endpoint;
            _logger.Info($"Starting SRS Data Connection");
            SrsClientSyncHandler.TryConnect(_endpoint, ConnectCallback);
        }

        public void ConnectAudio()
        {
            _logger.Info($"Starting SRS Audio Connection");
            UdpVoiceHandler = new UdpVoiceHandler(ShortGuid, _endpoint, _audioManager, this);
            var udpListenerThread = new Thread(UdpVoiceHandler.Listen) {Name = "Audio Listener"};
            udpListenerThread.Start();
        }

        private void ConnectCallback(bool result, bool connectionError, string connection)
        {
            if (result)
            {
                if (IsTcpConnected) return;

                IsTcpConnected = true;
                ConnectAudio();
                _audioManager.StartEncoding();
            }
            else
            {
                Disconnect();
                Thread.Sleep(5000);
                _logger.Debug("Could not connect to SRS server. Trying again");
                ConnectData(_endpoint);
            }
        }

        public void Disconnect()
        {
            IsTcpConnected = false;

            SrsClientSyncHandler.Disconnect();
            UdpVoiceHandler.RequestStop();

            DcsPlayerRadioInfo.Reset();
            PlayerCoalitionLocationMetadata.Reset();
        }

        #region ConnectedClientSingleton
        
        private readonly ConcurrentDictionary<string, SRClient> _clients = new ConcurrentDictionary<string, SRClient>();
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public SRClient this[string key]
        {
            get => _clients[key];
            set => _clients[key] = value;
        }

        public ICollection<SRClient> Values => _clients.Values;

        public bool TryRemove(string key, out SRClient value)
        {
            return _clients.TryRemove(key, out value);
        }

        public void Clear()
        {
            _clients.Clear();
        }

        public bool TryGetValue(string key, out SRClient value)
        {
            return _clients.TryGetValue(key, out value);
        }

        public bool ContainsKey(string key)
        {
            return _clients.ContainsKey(key);
        }

        public List<SRClient> ClientsOnFreq(double freq, RadioInformation.Modulation modulation)
        {
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.SHOW_TUNED_COUNT))
            {
                return new List<SRClient>();
            }
            var currentClientPos = PlayerCoalitionLocationMetadata;
            var currentUnitId = DcsPlayerRadioInfo.unitId;
            var coalitionSecurity = SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY);
            var globalFrequencies = _serverSettings.GlobalFrequencies;
            var global = globalFrequencies.Contains(freq);

            return (from client in _clients
                where !client.Key.Equals(ShortGuid)
                where global || !coalitionSecurity || client.Value.Coalition == currentClientPos.side
                let radioInfo = client.Value.RadioInfo 
                where radioInfo != null
                let receivingRadio = radioInfo.CanHearTransmission(freq, modulation, 0, currentUnitId, new List<int>(), out _, out _)
                where receivingRadio != null
                select client.Value).ToList();
        }

        #endregion
    }
}
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using RurouniJones.DCS.OverlordBot.Audio.Managers;
using RurouniJones.DCS.OverlordBot.Settings;
using NLog;

namespace RurouniJones.DCS.OverlordBot.Network
{
    public sealed class Client
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private readonly AudioManager _audioManager;

        public readonly SrsDataClient SrsDataClient;
        public SrsAudioClient SrsAudioClient;

        public DCSPlayerRadioInfo DcsPlayerRadioInfo { get; set; }
        public DCSPlayerSideInfo PlayerCoalitionLocationMetadata { get; set; }

        private IPEndPoint _endpoint;

        public long LastSent { get; set; }

        public bool IsDataConnected { get; set; }
        public bool IsAudioConnected { get; set; }

        public string ShortGuid { get; }

        // Indicates the user's desire to be in External Awacs Mode or not
        public bool ExternalAwacsModeSelected { get; set; }

        // Indicates whether we are *actually* connected in External Awacs Mode
        // Used by the Name and Password related UI elements to determine if they are editable or not
        public bool ExternalAwacsModeConnected { get; set; }

        public string LastSeenName { get; set; }

        public readonly string ExternalAwacsModePassword;

        public Client(AudioManager audioManager, DCSPlayerRadioInfo playerRadioInfo )
        {
            _audioManager = audioManager;
            ShortGuid = Ciribob.DCS.SimpleRadio.Standalone.Common.Network.ShortGuid.NewGuid();
            DcsPlayerRadioInfo = playerRadioInfo;
            PlayerCoalitionLocationMetadata = new DCSPlayerSideInfo();
            SrsDataClient = new SrsDataClient(this);

            ExternalAwacsModePassword = playerRadioInfo.radios.First().coalitionPassword;

            LastSent = 0;

            IsDataConnected = false;
            ExternalAwacsModeSelected = false;

            LastSeenName = playerRadioInfo.name;
        }

        public void ConnectData(IPEndPoint endpoint)
        {
            _endpoint = endpoint;
            _logger.Info($"Starting SRS Data Connection");
            SrsDataClient.TryConnect(_endpoint, DataConnectedCallback);
        }

        public void ConnectAudio()
        {
            _logger.Info($"Starting SRS Audio Connection");
            SrsAudioClient = new SrsAudioClient(ShortGuid, _endpoint, _audioManager, this);
            var udpListenerThread = new Thread(SrsAudioClient.Listen) {Name = "Audio Listener"};
            udpListenerThread.Start();
        }

        private void DataConnectedCallback(bool result)
        {
            if (result)
            {
                if (IsDataConnected) return;

                IsDataConnected = true;
                ConnectAudio();
                _audioManager.StartEncoding();
                SrsDataClient.ConnectExternalAwacsMode();
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
            _logger.Debug("Disconnecting from Server");

            IsDataConnected = false;
            IsAudioConnected = false;

            SrsDataClient.DisconnectExternalAwacsMode();
            SrsDataClient.Disconnect();
            SrsAudioClient.RequestStop();

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
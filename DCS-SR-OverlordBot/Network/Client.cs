using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Threading;
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

        public readonly AudioManager AudioManager;

        public readonly SrsDataClient SrsDataClient;
        public SrsAudioClient SrsAudioClient;

        public DCSPlayerRadioInfo DcsPlayerRadioInfo { get; set; }
        public DCSPlayerSideInfo PlayerCoalitionLocationMetadata { get; set; }

        public IPEndPoint Endpoint;

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

        public string LogClientId;

        private volatile bool _requestStop;

        private readonly DispatcherTimer _connectionMonitorTimer;


        // Various threads
        private Thread _clientSyncThread;
        private Thread _udpListenerThread;
        private Thread _udpPingThread;

        public Client(AudioManager audioManager, DCSPlayerRadioInfo playerRadioInfo )
        {
            AudioManager = audioManager;
            ShortGuid = Ciribob.DCS.SimpleRadio.Standalone.Common.Network.ShortGuid.NewGuid();
            DcsPlayerRadioInfo = playerRadioInfo;
            PlayerCoalitionLocationMetadata = new DCSPlayerSideInfo();
            SrsDataClient = new SrsDataClient(this);

            LogClientId = audioManager.LogClientId;

            ExternalAwacsModePassword = playerRadioInfo.radios.First().coalitionPassword;

            LastSent = 0;

            IsDataConnected = false;
            ExternalAwacsModeSelected = false;

            LastSeenName = playerRadioInfo.name;

            _connectionMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _connectionMonitorTimer.Tick += MonitorConnectionStatus;
        }

        public void MonitorConnectionStatus(object sender, EventArgs e)
        {
            if (IsDataConnected && IsAudioConnected || _requestStop) return;
            Reconnect();
        }

        public void ConnectData(IPEndPoint endpoint)
        {
            _requestStop = false;
            Endpoint = endpoint;
            _logger.Info($"{LogClientId}| Starting SRS Data Connection");
            IsDataConnected = SrsDataClient.Connect(endpoint);

            if (IsDataConnected)
            {
                _clientSyncThread = new Thread(SrsDataClient.ClientSyncLoop) {Name = $"{LogClientId} Client Sync Loop"};
                _clientSyncThread.Start();
                ConnectAudio();
                AudioManager.StartEncoding();
                SrsDataClient.ConnectExternalAwacsMode();
                Thread.Sleep(10000);
                _connectionMonitorTimer.Start();
            }
            else
            {
                _logger.Info($"{LogClientId}| SRS Data Connection failed");
                Reconnect();
            }
        }

        private void Reconnect()
        {
            _logger.Error(
                $"Connection Error. Data Connected {IsDataConnected}, Audio Connected {IsAudioConnected}, Stop Requested {_requestStop}");
            _logger.Debug($"{LogClientId}| Disconnecting");
            Disconnect();
            _logger.Debug($"{LogClientId}| Reconnecting");
            ConnectData(Endpoint);
        }

        public void ConnectAudio()
        {
            _logger.Info($"{LogClientId}| Starting SRS Audio Connection");
            SrsAudioClient = new SrsAudioClient(this);
            _udpListenerThread = new Thread(SrsAudioClient.Listen) {Name = $"{LogClientId} Audio Listener"};
            _udpListenerThread.Start();

            _udpPingThread = new Thread(SrsAudioClient.StartPing)  {Name = $"{LogClientId} Ping Threadr"};
            _udpPingThread.Start();

        }

        public void Disconnect()
        {
            _logger.Debug($"{LogClientId}| Disconnecting from Server");
            
            _connectionMonitorTimer.Stop();

            _requestStop = true;
            IsDataConnected = false;
            IsAudioConnected = false;

            DcsPlayerRadioInfo.LastUpdate = 0;
            Clear();

            SrsDataClient?.RequestStop();
            _clientSyncThread?.Join();
            _clientSyncThread = null;

            SrsAudioClient?.RequestStop();
            _udpListenerThread?.Join();
            _udpListenerThread = null;

            _udpPingThread?.Join();
            _udpPingThread = null;

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
            ExternalAwacsModeConnected = false;
            PlayerCoalitionLocationMetadata.side = 0;
            PlayerCoalitionLocationMetadata.name = "";
            DcsPlayerRadioInfo.name = "";
            DcsPlayerRadioInfo.LastUpdate = 0;
            LastSent = 0;
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

        public List<SRClient> GetHumanSrsClients()
        {
            var allClients = _clients.Values;
            return (from client in allClients where !client.Name.Contains("OverlordBot") && !client.Name.Contains("ATIS") && !client.Name.Contains("---") select client).ToList();
        }

        public List<string> GetHumanSrsClientNames()
        {
            var allClients = _clients.Values;
            return (from client in allClients where !client.Name.Contains("OverlordBot") && !client.Name.Contains("ATIS") && !client.Name.Contains("---") select client.Name).ToList();
        }

        public List<string> GetBotCallsignCompatibleClients()
        {
            var allClients = _clients.Values;
            return (from client in allClients where client.Name != "OverlordBot" && !client.Name.Contains("ATIS") && !client.Name.Contains("---") && IsClientNameCompatible(client.Name) select client.Name).ToList();
        }

        public bool IsClientNameCompatible(string name)
        {
            return Regex.Match(name, @"[a-zA-Z]{3,} \d-\d{1,2}").Success || Regex.Match(name, @"[a-zA-Z]{3,} \d{2,3}").Success;
        }

        public List<string> GetHumansOnFreq(RadioInformation radioInfo)
        {
            var clientsOnFreq = ClientsOnFreq(radioInfo.freq, radioInfo.modulation);
            return (from client in clientsOnFreq where client.Name != "OverlordBot" select client.Name).ToList();
        }

        #endregion
    }
}
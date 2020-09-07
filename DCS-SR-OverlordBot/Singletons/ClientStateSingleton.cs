using System;
using System.ComponentModel;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons
{
    public sealed class ClientStateSingleton : INotifyPropertyChanged
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static volatile ClientStateSingleton _instance;
        private static readonly object Lock = new object();

        public event PropertyChangedEventHandler PropertyChanged;

        public DCSPlayerRadioInfo DcsPlayerRadioInfo { get; }
        public DCSPlayerSideInfo PlayerCoalitionLocationMetadata { get; set; }

        // Timestamp the last UDP Game GUI broadcast was received from DCS, used for determining active game connection
        public long DcsGameGuiLastReceived { get; set; }

        // Timestamp the last UDP Export broadcast was received from DCS, used for determining active game connection
        public long DcsExportLastReceived { get; set; }

        // Timestamp for the last time 
        public long LotAtcLastReceived { get; set; }

        //store radio channels here?
        public PresetChannelsViewModel[] FixedChannels { get; }

        public long LastSent { get; set; }

        private static readonly DispatcherTimer Timer = new DispatcherTimer();

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                NotifyPropertyChanged("IsConnected");
            }
        }

        private bool _isVoipConnected;
        public bool IsVoipConnected
        {
            get => _isVoipConnected;
            set
            {
                _isVoipConnected = value;
                if(value)
                {
                    _logger.Debug($"Publishing Connection State {SrsClientSyncHandler.ConnectionState.Connected}");
                    //hub.Publish(SRSClientSyncHandler.ConnectionState.Connected);
                    SrsClientSyncHandler.Instance.ProcessConnectionState(SrsClientSyncHandler.ConnectionState.Connected);
                }
                if (value == false)
                {
                    _logger.Debug($"Publishing Connection State {SrsClientSyncHandler.ConnectionState.Disconnected}");
                    //hub.Publish(SRSClientSyncHandler.ConnectionState.Disconnected);
                    SrsClientSyncHandler.Instance.ProcessConnectionState(SrsClientSyncHandler.ConnectionState.Disconnected);
                }
                NotifyPropertyChanged("IsVoipConnected");
            }
        }

        private bool _isConnectionErrored;
        public string ShortGuid { get; }

        public bool IsConnectionErrored
        {
            get => _isConnectionErrored;
            set
            {
                _isConnectionErrored = value;
                NotifyPropertyChanged("isConnectionErrored");
            }
        }

        // Indicates the user's desire to be in External Awacs Mode or not
        public bool ExternalAwacsModeSelected { get; set; }

        // Indicates whether we are *actually* connected in External Awacs Mode
        // Used by the Name and Password related UI elements to determine if they are editable or not
        public bool ExternalAwacsModeConnected { get; set; }
        /*{
            get
            {
                bool EamEnabled = SyncedServerSettings.Instance.GetSettingAsBool(Common.Setting.ServerSettingsKeys.EXTERNAL_AWACS_MODE);
                return IsConnected && EamEnabled && ExternalAWACSModeSelected && !IsGameExportConnected;
            }
        }*/

        public string LastSeenName { get; set; }

        public string ExternalAwacsModePassword { get; set; }

        public bool IsGameExportConnected => DcsExportLastReceived >= DateTime.Now.Ticks - 100000000;

        private ClientStateSingleton()
        {
            ShortGuid = Common.Network.ShortGuid.NewGuid();
            DcsPlayerRadioInfo = new DCSPlayerRadioInfo();
            PlayerCoalitionLocationMetadata = new DCSPlayerSideInfo();

            // The following members are not updated due to events. Therefore we need to setup a polling action so that they are
            // periodically checked.
            DcsGameGuiLastReceived = 0;
            DcsExportLastReceived = 0;
            Timer.Interval = TimeSpan.FromSeconds(1);
            Timer.Tick += (s, e) => {
                NotifyPropertyChanged("IsGameConnected");
                NotifyPropertyChanged("IsLotATCConnected");
                NotifyPropertyChanged("ExternalAWACSModeConnected");
            };
            Timer.Start();

            FixedChannels = new PresetChannelsViewModel[10];

            for (var i = 0; i < FixedChannels.Length; i++)
            {
                FixedChannels[i] = new PresetChannelsViewModel(new FilePresetChannelsStore(), i + 1);
            }

            LastSent = 0;

            IsConnected = false;
            ExternalAwacsModeSelected = false;

            LastSeenName = "OverlordBot";
        }

        public static ClientStateSingleton Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (Lock)
                {
                    if (_instance == null)
                        _instance = new ClientStateSingleton();
                }

                return _instance;
            }
        }

        private void NotifyPropertyChanged(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
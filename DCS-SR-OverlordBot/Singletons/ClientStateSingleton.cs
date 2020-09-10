using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons
{
    public sealed class ClientStateSingleton
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static volatile ClientStateSingleton _instance;
        private static readonly object Lock = new object();

        public DCSPlayerRadioInfo DcsPlayerRadioInfo { get; }
        public DCSPlayerSideInfo PlayerCoalitionLocationMetadata { get; set; }

        //store radio channels here?
        public PresetChannelsViewModel[] FixedChannels { get; }

        public long LastSent { get; set; }

        public bool IsTcpConnected { get; set; }

        private bool _isVoipConnected;
        public bool IsVoipConnected
        {
            get => _isVoipConnected;
            set
            {
                _isVoipConnected = value;
                if(value)
                {
                    _logger.Debug($"Connection State {SrsClientSyncHandler.ConnectionState.Connected}");
                    SrsClientSyncHandler.Instance.ProcessConnectionState(SrsClientSyncHandler.ConnectionState.Connected);
                }
                if (value == false)
                {
                    _logger.Debug($"Connection State {SrsClientSyncHandler.ConnectionState.Disconnected}");
                    SrsClientSyncHandler.Instance.ProcessConnectionState(SrsClientSyncHandler.ConnectionState.Disconnected);
                }
            }
        }

        public string ShortGuid { get; }

        public bool IsConnectionErrored { get; set; }

        // Indicates the user's desire to be in External Awacs Mode or not
        public bool ExternalAwacsModeSelected { get; set; }

        // Indicates whether we are *actually* connected in External Awacs Mode
        // Used by the Name and Password related UI elements to determine if they are editable or not
        public bool ExternalAwacsModeConnected { get; set; }

        public string LastSeenName { get; set; }

        public string ExternalAwacsModePassword { get; set; }

        private ClientStateSingleton()
        {
            ShortGuid = Common.Network.ShortGuid.NewGuid();
            DcsPlayerRadioInfo = new DCSPlayerRadioInfo();
            PlayerCoalitionLocationMetadata = new DCSPlayerSideInfo();

            FixedChannels = new PresetChannelsViewModel[10];

            for (var i = 0; i < FixedChannels.Length; i++)
            {
                FixedChannels[i] = new PresetChannelsViewModel(new FilePresetChannelsStore(), i + 1);
            }

            LastSent = 0;

            IsTcpConnected = false;
            ExternalAwacsModeSelected = false;

            LastSeenName = "OverlordBot-Development";
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
    }
}
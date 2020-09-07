using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for ServerSettingsWindow.xaml
    /// </summary>
    public partial class ServerSettingsWindow
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _updateTimer;

        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public ServerSettingsWindow()
        {
            InitializeComponent();

            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            _updateTimer.Tick += UpdateUi;
            _updateTimer.Start();

            UpdateUi(null, null);
        }

        private void UpdateUi(object sender, EventArgs e)
        {
            var settings = _serverSettings;

            try
            {
                SpectatorAudio.Content = settings.GetSettingAsBool(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED)
                    ? "DISABLED"
                    : "ENABLED";

                CoalitionSecurity.Content = settings.GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY)
                    ? "ON"
                    : "OFF";

                LineOfSight.Content = settings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED) ? "ON" : "OFF";

                Distance.Content = settings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ? "ON" : "OFF";

                RealRadio.Content = settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX) ? "ON" : "OFF";

                RadioRxInterference.Content =
                    settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE) ? "ON" : "OFF";

                RadioExpansion.Content = settings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION) ? "ON" : "OFF";

                ExternalAwacsMode.Content = settings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE) ? "ON" : "OFF";

                AllowRadioEncryption.Content = settings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION) ? "ON" : "OFF";

                ServerVersion.Content = SrsClientSyncHandler.ServerVersion;
            }
            catch (IndexOutOfRangeException)
            {
                Logger.Warn("Missing Server Option - Connected to old server");
            }
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _updateTimer.Stop();
        }
    }
}
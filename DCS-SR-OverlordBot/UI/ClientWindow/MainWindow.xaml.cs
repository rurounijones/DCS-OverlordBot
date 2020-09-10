using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Windows;
using System.Windows.Forms;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NLog;
using MessageBox = System.Windows.MessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private static readonly AudioManager AudioManager = new AudioManager();

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly int _port;

        private readonly IPAddress _resolvedIp;

        private readonly SettingsStore _settings = SettingsStore.Instance;

        /// <remarks>Used in the XAML for DataBinding many things</remarks>
        public Network.Client ClientState { get; } = AudioManager.Client;

        public MainWindow()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            InitializeComponent();

            DataContext = this;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = _settings.GetPositionSetting(SettingsKeys.ClientX).DoubleValue;
            Top = _settings.GetPositionSetting(SettingsKeys.ClientY).DoubleValue;

            Title += " - 1.11.2.0";

            CheckWindowVisibility();

            if (_settings.GetClientSetting(SettingsKeys.StartMinimised).BoolValue)
            {
                Hide();
                WindowState = WindowState.Minimized;

                _logger.Info("Started DCS-SimpleRadio Client " + UpdaterChecker.VERSION + " minimized");
            }
            else
            {
                _logger.Info("Started DCS-SimpleRadio Client " + UpdaterChecker.VERSION);
            }

            ExternalAwacsModeName.Text = _settings.GetClientSetting(SettingsKeys.LastSeenName).StringValue;
            
            _logger.Debug("Connecting on Startup");

            

            var resolvedAddresses = Dns.GetHostAddresses(Properties.Settings.Default.SRSHost);
            _resolvedIp = resolvedAddresses.FirstOrDefault(xa => xa.AddressFamily == AddressFamily.InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

            if (_resolvedIp == null)
            {
                throw new Exception($"Could not determine IPv4 address for {Properties.Settings.Default.SRSHost}");
            }

            _port = Properties.Settings.Default.SRSPort;

            ServerName.Text = Properties.Settings.Default.SRSHostId;
            ServerEndpoint.Text = Properties.Settings.Default.SRSHost + ":" + _port;

            Connect();
        }

        private void CheckWindowVisibility()
        {
            if (_settings.GetPositionSetting(SettingsKeys.DisableWindowVisibilityCheck).BoolValue)
            {
                _logger.Info("Window visibility check is disabled, skipping");
                return;
            }

            var mainWindowVisible = false;

            var mainWindowX = (int)_settings.GetPositionSetting(SettingsKeys.ClientX).DoubleValue;
            var mainWindowY = (int)_settings.GetPositionSetting(SettingsKeys.ClientY).DoubleValue;

            _logger.Info($"Checking window visibility for main client window {{X={mainWindowX},Y={mainWindowY}}}");

            foreach (var screen in Screen.AllScreens)
            {
                _logger.Info($"Checking {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds} for window visibility");

                if (screen.Bounds.Contains(mainWindowX, mainWindowY))
                {
                    _logger.Info($"Main client window {{X={mainWindowX},Y={mainWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    mainWindowVisible = true;
                }
            }

            if (!mainWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS client window is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue.",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _logger.Warn($"Main client window outside visible area of monitors, resetting position ({mainWindowX},{mainWindowY}) to defaults");

                _settings.SetPositionSetting(SettingsKeys.ClientX, 200);
                _settings.SetPositionSetting(SettingsKeys.ClientY, 200);

                Left = 200;
                Top = 200;
            }
            
            if (mainWindowVisible)
            {
                _settings.Save();
            }
        }

        private void Connect()
        {
            if (ClientState.IsTcpConnected)
            {
                ClientState.Disconnect();
            }
            else
            {
                try
                {
                    AudioManager.ConnectToSRS(new IPEndPoint(_resolvedIp, _port));
                }
                catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
                {
                    MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void Stop()
        {
            AudioManager.StopEncoding();
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            _settings.SetPositionSetting(SettingsKeys.ClientX, Left);
            _settings.SetPositionSetting(SettingsKeys.ClientY, Top);

            if (!string.IsNullOrWhiteSpace(ClientState.LastSeenName) &&
                _settings.GetClientSetting(SettingsKeys.LastSeenName).StringValue != ClientState.LastSeenName)
            {
                _settings.SetClientSetting(SettingsKeys.LastSeenName, ClientState.LastSeenName);
            }

            //save window position
            base.OnClosing(e);

            Stop();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _settings.GetClientSetting(SettingsKeys.MinimiseToTray).BoolValue)
            {
                Hide();
            }

            base.OnStateChanged(e);
        }
    }
}

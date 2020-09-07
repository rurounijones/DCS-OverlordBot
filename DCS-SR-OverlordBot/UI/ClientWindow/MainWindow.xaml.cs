using System;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Easy.MessageHub;
using NLog;
using MessageBox = System.Windows.MessageBox;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private AudioManager _audioManager;

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private SrsClientSyncHandler _client;
        private int _port = 5002;

        private Overlay.RadioOverlayWindow _radioOverlayWindow;
        private AwacsRadioOverlayWindow.RadioOverlayWindow _awacsRadioOverlay;

        private IPAddress _resolvedIp;
        private ServerSettingsWindow _serverSettingsWindow;

        //used to debounce toggle
        private long _toggleShowHide;

        private readonly DispatcherTimer _redrawUiTimer;
        private ServerAddress _serverAddress;
        private readonly DelegateCommand _connectCommand;

        private readonly SettingsStore _settings = SettingsStore.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        /// <remarks>Used in the XAML for DataBinding many things</remarks>
        public ClientStateSingleton ClientState { get; } = ClientStateSingleton.Instance;

        private readonly IMessageHub _hub = MessageHub.Instance;

        public MainWindow()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            InitializeComponent();

            // Initialize ToolTip controls
            ToolTips.Init();

            // Initialize images/icons
            Images.Init();

            // Set up tooltips that are always defined
            InitToolTips();

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

            _connectCommand = new DelegateCommand(Connect, () => ServerAddress != null);
            FavouriteServersViewModel = new FavouriteServersViewModel(new CsvFavouriteServerStore());

            InitDefaultAddress();

            ExternalAwacsModeName.Text = _settings.GetClientSetting(SettingsKeys.LastSeenName).StringValue;

            _redrawUiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _redrawUiTimer.Tick += RedrawUiTick;
            _redrawUiTimer.Start();

            _logger.Debug("Connecting on Startup");
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
            var radioWindowVisible = false;
            var awacsWindowVisible = false;

            var mainWindowX = (int)_settings.GetPositionSetting(SettingsKeys.ClientX).DoubleValue;
            var mainWindowY = (int)_settings.GetPositionSetting(SettingsKeys.ClientY).DoubleValue;
            var radioWindowX = (int)_settings.GetPositionSetting(SettingsKeys.RadioX).DoubleValue;
            var radioWindowY = (int)_settings.GetPositionSetting(SettingsKeys.RadioY).DoubleValue;
            var awacsWindowX = (int)_settings.GetPositionSetting(SettingsKeys.AwacsX).DoubleValue;
            var awacsWindowY = (int)_settings.GetPositionSetting(SettingsKeys.AwacsY).DoubleValue;

            _logger.Info($"Checking window visibility for main client window {{X={mainWindowX},Y={mainWindowY}}}");
            _logger.Info($"Checking window visibility for radio overlay {{X={radioWindowX},Y={radioWindowY}}}");
            _logger.Info($"Checking window visibility for AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}}");

            foreach (var screen in Screen.AllScreens)
            {
                _logger.Info($"Checking {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds} for window visibility");

                if (screen.Bounds.Contains(mainWindowX, mainWindowY))
                {
                    _logger.Info($"Main client window {{X={mainWindowX},Y={mainWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    mainWindowVisible = true;
                }
                if (screen.Bounds.Contains(radioWindowX, radioWindowY))
                {
                    _logger.Info($"Radio overlay {{X={radioWindowX},Y={radioWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    radioWindowVisible = true;
                }

                if (!screen.Bounds.Contains(awacsWindowX, awacsWindowY)) continue;
                _logger.Info($"AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                awacsWindowVisible = true;
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

            if (!radioWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS radio overlay is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue.",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _logger.Warn($"Radio overlay window outside visible area of monitors, resetting position ({radioWindowX},{radioWindowY}) to defaults");

                _settings.SetPositionSetting(SettingsKeys.RadioX, 300);
                _settings.SetPositionSetting(SettingsKeys.RadioY, 300);

                if (_radioOverlayWindow != null)
                {
                    _radioOverlayWindow.Left = 300;
                    _radioOverlayWindow.Top = 300;
                }
            }

            if (!awacsWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS AWACS overlay is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _logger.Warn($"AWACS overlay window outside visible area of monitors, resetting position ({awacsWindowX},{awacsWindowY}) to defaults");

                _settings.SetPositionSetting(SettingsKeys.AwacsX, 300);
                _settings.SetPositionSetting(SettingsKeys.AwacsY, 300);

                if (_awacsRadioOverlay != null)
                {
                    _awacsRadioOverlay.Left = 300;
                    _awacsRadioOverlay.Top = 300;
                }
            }

            if (!mainWindowVisible || !radioWindowVisible || !awacsWindowVisible)
            {
                _settings.Save();
            }
        }

        private void InitDefaultAddress()
        {
            // legacy setting migration
            if (!string.IsNullOrEmpty(_settings.GetClientSetting(SettingsKeys.LastServer).StringValue) &&
                FavouriteServersViewModel.Addresses.Count == 0)
            {
                var oldAddress = new ServerAddress(_settings.GetClientSetting(SettingsKeys.LastServer).StringValue,
                    _settings.GetClientSetting(SettingsKeys.LastServer).StringValue, null, true);
                FavouriteServersViewModel.Addresses.Add(oldAddress);
            }

            ServerAddress = FavouriteServersViewModel.DefaultServerAddress;
        }

        private void InitToolTips()
        {
            ExternalAwacsModePassword.ToolTip = ToolTips.ExternalAwacsModePassword;
            ExternalAwacsModeName.ToolTip = ToolTips.ExternalAwacsModeName;
        }

        public FavouriteServersViewModel FavouriteServersViewModel { get; }

        public ServerAddress ServerAddress
        {
            get => _serverAddress;
            set
            {
                _serverAddress = value;
                if (value != null)
                {
                    ServerIp.Text = value.Address;
                    ClientState.ExternalAwacsModePassword = string.IsNullOrWhiteSpace(value.EamCoalitionPassword) ? "" : value.EamCoalitionPassword;
                }

                _connectCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand ConnectCommand => _connectCommand;

        private void RedrawUiTick(object sender, EventArgs e)
        {
            var isGameExportConnected = ClientState.IsGameExportConnected;

            // Redraw UI state (currently once per second), toggling controls as required
            // Some other callbacks/UI state changes could also probably be moved to this...
            if (ClientState.IsConnected)
            {
                var eamEnabled = _serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE);

                ExternalAwacsModePassword.IsEnabled = eamEnabled && !ClientState.ExternalAwacsModeConnected && !isGameExportConnected;
                ExternalAwacsModeName.IsEnabled = eamEnabled && !ClientState.ExternalAwacsModeConnected && !isGameExportConnected;
            }
            else
            {
                ExternalAwacsModePassword.IsEnabled = false;
                ExternalAwacsModeName.IsEnabled = false;
            }
        }

        private void Connect()
        {
            if (ClientState.IsConnected)
            {
                Stop();
            }
            else
            {
                _audioManager = AudioManager.Instance;

                try
                {
                    //process hostname
                    var resolvedAddresses = Dns.GetHostAddresses(GetAddressFromTextBox());
                    var ip = resolvedAddresses.FirstOrDefault(xa => xa.AddressFamily == AddressFamily.InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

                    if (ip != null)
                    {
                        _resolvedIp = ip;
                        _port = GetPortFromTextBox();

                        _client = SrsClientSyncHandler.Instance;
                        _client.TryConnect(new IPEndPoint(_resolvedIp, _port), ConnectCallback);

                        StartStop.Content = "Connecting...";
                        StartStop.IsEnabled = false;
                    }
                    else
                    {
                        //invalid ID
                        MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        ClientState.IsConnected = false;
                        ToggleServerSettings.IsEnabled = false;
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
                {
                    MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    ClientState.IsConnected = false;
                    ToggleServerSettings.IsEnabled = false;
                }
            }
        }

        private string GetAddressFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            return addr.Contains(":") ? addr.Split(':')[0] : addr;
        }

        private int GetPortFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            if (!addr.Contains(":")) return 5002;
            if (int.TryParse(addr.Split(':')[1], out var port))
            {
                return port;
            }
            throw new ArgumentException("specified port is not valid");

        }

        private void Stop()
        {
            StartStop.Content = "Connect";
            StartStop.IsEnabled = true;
            ClientState.IsConnected = false;
            ToggleServerSettings.IsEnabled = false;

            ExternalAwacsModePassword.IsEnabled = false;
            ExternalAwacsModePasswordLabel.IsEnabled = false;
            ExternalAwacsModeName.IsEnabled = false;
            ExternalAwacsModeNameLabel.IsEnabled = false;

            if (!string.IsNullOrWhiteSpace(ClientState.LastSeenName) &&
                _settings.GetClientSetting(SettingsKeys.LastSeenName).StringValue != ClientState.LastSeenName)
            {
                _settings.SetClientSetting(SettingsKeys.LastSeenName, ClientState.LastSeenName);
            }

            if (_audioManager != null)
            {
                _audioManager.StopEncoding();
                _audioManager = null;
            }

            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }

            ClientState.DcsPlayerRadioInfo.Reset();
            ClientState.PlayerCoalitionLocationMetadata.Reset();

            _logger.Debug("Could not connect to SRS server. Trying again");
            Thread.Sleep(5000);
            Connect();
        }

        private void ConnectCallback(bool result, bool connectionError, string connection)
        {
            if (result)
            {
                if (ClientState.IsConnected) return;
                StartStop.Content = "Disconnect";
                StartStop.IsEnabled = true;

                ClientState.IsConnected = true;

                _settings.SetClientSetting(SettingsKeys.LastServer, ServerIp.Text);

                _audioManager.StartEncoding(_settings.GetClientSetting(SettingsKeys.CliendIdShort).StringValue, _resolvedIp, _port);
            }
            else
            {
                if (ClientState.IsConnected) return;
                Stop();
                _hub.Publish(SrsClientSyncHandler.ConnectionState.Disconnected);
            }
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

            // Stop UI redraw timer
            _redrawUiTimer?.Stop();

            Stop();

            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;

            _awacsRadioOverlay?.Close();
            _awacsRadioOverlay = null;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _settings.GetClientSetting(SettingsKeys.MinimiseToTray).BoolValue)
            {
                Hide();
            }

            base.OnStateChanged(e);
        }

        private void ShowOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleOverlay(true);
        }

        private void ToggleOverlay(bool uiButton)
        {
            //debounce show hide (1 tick = 100ns, 6000000 ticks = 600ms debounce)
            if (DateTime.Now.Ticks - _toggleShowHide <= 6000000 && !uiButton) return;
            _toggleShowHide = DateTime.Now.Ticks;
            if (_radioOverlayWindow == null || !_radioOverlayWindow.IsVisible ||
                _radioOverlayWindow.WindowState == WindowState.Minimized)
            {
                //hide awacs panel
                _awacsRadioOverlay?.Close();
                _awacsRadioOverlay = null;

                _radioOverlayWindow?.Close();

                _radioOverlayWindow = new Overlay.RadioOverlayWindow
                {
                    ShowInTaskbar = !_settings.GetClientSetting(SettingsKeys.RadioOverlayTaskbarHide).BoolValue
                };


                _radioOverlayWindow.Show();
            }
            else
            {
                _radioOverlayWindow?.Close();
                _radioOverlayWindow = null;
            }
        }

        private void ShowAwacsOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            if (_awacsRadioOverlay == null || !_awacsRadioOverlay.IsVisible ||
                _awacsRadioOverlay.WindowState == WindowState.Minimized)
            {
                //close normal overlay
                _radioOverlayWindow?.Close();
                _radioOverlayWindow = null;

                _awacsRadioOverlay?.Close();

                _awacsRadioOverlay = new AwacsRadioOverlayWindow.RadioOverlayWindow
                {
                    ShowInTaskbar = !_settings.GetClientSetting(SettingsKeys.RadioOverlayTaskbarHide).BoolValue
                };
                _awacsRadioOverlay.Show();
            }
            else
            {
                _awacsRadioOverlay?.Close();
                _awacsRadioOverlay = null;
            }
        }

        private void ToggleServerSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if (_serverSettingsWindow == null || !_serverSettingsWindow.IsVisible ||
                _serverSettingsWindow.WindowState == WindowState.Minimized)
            {
                _serverSettingsWindow?.Close();

                _serverSettingsWindow = new ServerSettingsWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this
                };
                _serverSettingsWindow.ShowDialog();
            }
            else
            {
                _serverSettingsWindow?.Close();
                _serverSettingsWindow = null;
            }
        }
        
        private void LaunchAddressTab(object sender, RoutedEventArgs e)
        {
            TabControl.SelectedItem = FavouritesSeversTab;
        }
    }
}

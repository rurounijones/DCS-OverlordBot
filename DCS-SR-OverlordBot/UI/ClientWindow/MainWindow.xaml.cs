using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Windows;
using RurouniJones.DCS.OverlordBot.Audio.Managers;
using RurouniJones.DCS.OverlordBot.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Newtonsoft.Json;
using NLog;

namespace RurouniJones.DCS.OverlordBot.UI
{
    public partial class MainWindow
    {
        private static readonly List<AudioManager> AudioManagers = new List<AudioManager>();
        public static readonly string AwacsRadiosFile = "awacs-radios.json";

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly int _port;

        private readonly IPAddress _resolvedIp;

        private readonly SettingsStore _settings = SettingsStore.Instance;

        public MainWindow()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = _settings.GetPositionSetting(SettingsKeys.ClientX).DoubleValue;
            Top = _settings.GetPositionSetting(SettingsKeys.ClientY).DoubleValue;
            Title += " - 1.12.0.0";

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

            var radioJson = File.ReadAllText(AwacsRadiosFile);
            var awacsRadios = JsonConvert.DeserializeObject<List<RadioInformation>>(radioJson);

            foreach (var radio in awacsRadios)
            {
                var playerRadioInfo = new DCSPlayerRadioInfo
                {
                    LastUpdate = DateTime.Now.Ticks,
                    name = radio.name,
                    ptt = false,
                    radios = new List<RadioInformation> { radio },
                    selected = 1,
                    latLng = new DCSLatLngPosition {lat = 0, lng = 0, alt = 0},
                    simultaneousTransmission = false,
                    simultaneousTransmissionControl = DCSPlayerRadioInfo.SimultaneousTransmissionControl.ENABLED_INTERNAL_SRS_CONTROLS,
                    unit = "External AWACS",
                    unitId = 100000001,
                    inAircraft = false,
                    iff = new Transponder()
                };

                var audioManager = new AudioManager(playerRadioInfo);
                audioManager.ConnectToSrs(new IPEndPoint(_resolvedIp, _port));
                AudioManagers.Add(audioManager);
            }
        }

        private static void Stop()
        {
            foreach (var audioManager in AudioManagers)
            {
                audioManager.StopEncoding();
            }
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            _settings.SetPositionSetting(SettingsKeys.ClientX, Left);
            _settings.SetPositionSetting(SettingsKeys.ClientY, Top);

            //save window position
            base.OnClosing(e);

            Stop();
        }
    }
}

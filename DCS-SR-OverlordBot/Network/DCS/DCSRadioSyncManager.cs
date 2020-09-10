using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS
{
    public class DcsRadioSyncManager
    {
        private readonly SendRadioUpdate _clientRadioUpdate;
        public static readonly string AwacsRadiosFile = "awacs-radios.json";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Client _client;
        private readonly DcsRadioSyncHandler _dcsRadioSyncHandler;

        public delegate void SendRadioUpdate();

        private volatile bool _stopExternalAwacsMode;

        private readonly DispatcherTimer _clearRadio;

        public bool IsListening { get; private set; }

        public DcsRadioSyncManager(SendRadioUpdate clientRadioUpdate, Client client)
        {
            _clientRadioUpdate = clientRadioUpdate;
            IsListening = false;
            _client = client;
            _dcsRadioSyncHandler = new DcsRadioSyncHandler(clientRadioUpdate, _client);

            _clearRadio = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            _clearRadio.Tick += CheckIfRadioIsStale;
           
        }

        private void CheckIfRadioIsStale(object sender, EventArgs e)
        {
            if (_client.DcsPlayerRadioInfo.IsCurrent() || _client.DcsPlayerRadioInfo.LastUpdate <= 0) return;
            _client.PlayerCoalitionLocationMetadata.Reset();
            _client.DcsPlayerRadioInfo.Reset();

            _clientRadioUpdate();
            Logger.Info("Reset Radio state - no longer connected");
        }

        public void Start()
        {
            DcsListener();
            IsListening = true;
        }

        public void StartExternalAwacsModeLoop()
        {
            _stopExternalAwacsMode = false;

            RadioInformation[] awacsRadios;
            try
            {
                var radioJson = File.ReadAllText(AwacsRadiosFile);
                awacsRadios = JsonConvert.DeserializeObject<RadioInformation[]>(radioJson);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load AWACS radio file");

                awacsRadios = new RadioInformation[11];
                for (var i = 0; i < 11; i++)
                {
                    awacsRadios[i] = new RadioInformation
                    {
                        freq = 1,
                        freqMin = 1,
                        freqMax = 1,
                        secFreq = 0,
                        modulation = RadioInformation.Modulation.DISABLED,
                        name = "No Radio",
                        freqMode = RadioInformation.FreqMode.COCKPIT,
                        encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION,
                        volMode = RadioInformation.VolumeMode.COCKPIT
                    };
                }
            }

            // Force an immediate update of radio information
            _client.LastSent = 0;

            Task.Factory.StartNew(() =>
            {
                Logger.Debug("Starting external AWACS mode loop");
                _client.ExternalAwacsModeConnected = true;

                while (!_stopExternalAwacsMode)
                {
                    _dcsRadioSyncHandler.ProcessRadioInfo(new DCSPlayerRadioInfo
                    {
                        LastUpdate = 0,
                        control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS,
                        name = _client.LastSeenName,
                        ptt = false,
                        radios = awacsRadios,
                        selected = 1,
                        latLng = new DCSLatLngPosition {lat =0,lng=0,alt=0},
                        simultaneousTransmission = false,
                        simultaneousTransmissionControl = DCSPlayerRadioInfo.SimultaneousTransmissionControl.ENABLED_INTERNAL_SRS_CONTROLS,
                        unit = "External AWACS",
                        unitId = 100000001,
                        inAircraft = false
                    });

                    Thread.Sleep(200);
                }

                var radio = new DCSPlayerRadioInfo();
                radio.Reset();
                _dcsRadioSyncHandler.ProcessRadioInfo(radio);

                _client.ExternalAwacsModeConnected = false;
                Logger.Debug("Stopping external AWACS mode loop");
            });
        }

        public void StopExternalAwacsModeLoop()
        {
            _stopExternalAwacsMode = true;
        }

        private void DcsListener()
        {
             _clearRadio.Start();
        }

        public void Stop()
        {
            _stopExternalAwacsMode = true;
            IsListening = false;

            _clearRadio.Stop();
        }
    }
}
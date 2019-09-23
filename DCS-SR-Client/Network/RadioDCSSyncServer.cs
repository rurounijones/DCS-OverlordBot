using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.AwacsRadioOverlayWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Newtonsoft.Json;
using NLog;

/**
Keeps radio information in Sync Between DCS and 

**/

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class RadioDCSSyncServer
    {
        public static readonly string AWACS_RADIOS_FILE = "awacs-radios.json";

        public delegate void ClientSideUpdate();

        public delegate void SendRadioUpdate();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly SendRadioUpdate _clientRadioUpdate;
        private readonly ConcurrentDictionary<string, SRClient> _clients;
        private readonly ClientSideUpdate _clientSideUpdate;
        private readonly string _guid;

        private UdpClient _dcsGameGuiudpListener;

        private UdpClient _dcsLOSListener;
        private UdpClient _dcsUdpListener;
        private UdpClient _dcsRadioUpdateSender;
        private UdpClient _udpCommandListener;

        private volatile bool _stop;
        private volatile bool _stopExternalAWACSMode;

        private ClientStateSingleton _clientStateSingleton;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public bool IsListening { get; private set; }

        public RadioDCSSyncServer(SendRadioUpdate clientRadioUpdate, ClientSideUpdate clientSideUpdate,
            ConcurrentDictionary<string, SRClient> _clients, string guid)
        {
            _clientRadioUpdate = clientRadioUpdate;
            _clientSideUpdate = clientSideUpdate;
            this._clients = _clients;
            _guid = guid;
            _clientStateSingleton = ClientStateSingleton.Instance;
            IsListening = false;
        }

        private readonly SettingsStore _settings = SettingsStore.Instance;

        public void Listen()
        {
            DcsListener();
            IsListening = true;
        }

        public void StartExternalAWACSModeLoop()
        {
            _stopExternalAWACSMode = false;

            RadioInformation[] awacsRadios;
            try
            {
                string radioJson = File.ReadAllText(AWACS_RADIOS_FILE);
                awacsRadios = JsonConvert.DeserializeObject<RadioInformation[]>(radioJson);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load AWACS radio file");

                awacsRadios = new RadioInformation[11];
                for (int i = 0; i < 11; i++)
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
            _clientStateSingleton.LastSent = 0;

            Task.Factory.StartNew(() =>
            {
                Logger.Debug("Starting external AWACS mode loop");

                while (!_stopExternalAWACSMode)
                {
                    ProcessRadioInfo(new DCSPlayerRadioInfo
                    {
                        LastUpdate = 0,
                        control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS,
                        name = _clientStateSingleton.LastSeenName,
                        pos = new DcsPosition { x = 0, y = 0, z = 0 },
                        ptt = false,
                        radios = awacsRadios,
                        selected = 1,
                        simultaneousTransmission = false,
                        unit = "External AWACS",
                        unitId = 100000001
                    });

                    Thread.Sleep(200);
                }

                Logger.Debug("Stopping external AWACS mode loop");
            });
        }

        public void StopExternalAWACSModeLoop()
        {
            _stopExternalAWACSMode = true;
        }

        private void DcsListener()
        {
        }

        //send updated radio info back to DCS for ingame GUI
        private void SendRadioUpdateToDCS()
        {
            if (_dcsRadioUpdateSender == null)
            {
                _dcsRadioUpdateSender = new UdpClient
                {
                    ExclusiveAddressUse = false,
                    EnableBroadcast = true
                };
                _dcsRadioUpdateSender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                    true);
                _dcsRadioUpdateSender.ExclusiveAddressUse = false;
            }

            int clientCountIngame = 0;

            foreach (KeyValuePair<string, SRClient> kvp in _clients)
            {
                if (kvp.Value.IsIngame())
                {
                    clientCountIngame++;
                }
            }

            try
            {
                //get currently transmitting or receiving
                var combinedState = new CombinedRadioState()
                {
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                    RadioSendingState = TCPVoiceHandler.RadioSendingState,
                    RadioReceivingState = TCPVoiceHandler.RadioReceivingState,
                    ClientCountConnected = _clients.Count,
                    ClientCountIngame = clientCountIngame
                };

                var message = JsonConvert.SerializeObject(combinedState, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                }) + "\n";

                var byteData =
                    Encoding.UTF8.GetBytes(message);

                //Logger.Info("Sending Update over UDP 7080 DCS - 7082 Flight Panels: \n"+message);

                _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                        _settings.GetNetworkSetting(SettingsKeys.OutgoingDCSUDPInfo))); //send to DCS
                _dcsRadioUpdateSender.Send(byteData, byteData.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                        _settings.GetNetworkSetting(SettingsKeys
                            .OutgoingDCSUDPOther))); // send to Flight Control Panels
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending DCS Radio Update Message");
            }
        }

        private List<DCSLosCheckRequest> GenerateDcsLosCheckRequests()
        {
            var clients = _clients.Values.ToList();

            var requests = new List<DCSLosCheckRequest>();

            if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED))
            {
                foreach (var client in clients)
                {
                    //only check if its worth it
                    if ((client.Position.x != 0) && (client.Position.z != 0) && (client.ClientGuid != _guid))
                    {
                        requests.Add(new DCSLosCheckRequest
                        {
                            id = client.ClientGuid,
                            x = client.Position.x,
                            y = client.Position.y,
                            z = client.Position.z
                        });
                    }
                }
            }
            return requests;
        }

        private void ProcessRadioInfo(DCSPlayerRadioInfo message)
        {
            var update = UpdateRadio(message);

            //send to DCS UI
            SendRadioUpdateToDCS();

            Logger.Debug("Update sent to DCS");

            if (update || IsRadioInfoStale(message))
            {
                Logger.Debug("Sending Radio Info To Server - Stale");
                _clientStateSingleton.LastSent = DateTime.Now.Ticks;
                _clientRadioUpdate();
            }
        }

        private bool UpdateRadio(DCSPlayerRadioInfo message)
        {
            var changed = false;


            var expansion = _serverSettings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION);

            var playerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

            //update common parts
            playerRadioInfo.name = message.name;
           

            if (_settings.GetClientSetting(SettingsKeys.AlwaysAllowHotasControls).BoolValue)
            {
                message.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
                playerRadioInfo.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
            }
            else
            {
                playerRadioInfo.control = message.control;
            }

            playerRadioInfo.unit = message.unit;
            playerRadioInfo.pos = message.pos;

            var overrideFreqAndVol = false;

            var newAircraft = playerRadioInfo.unitId != message.unitId || !playerRadioInfo.IsCurrent();

            if (message.unitId >= DCSPlayerRadioInfo.UnitIdOffset &&
                playerRadioInfo.unitId >= DCSPlayerRadioInfo.UnitIdOffset)
            {
                //overriden so leave as is
            }
            else
            {
                overrideFreqAndVol = playerRadioInfo.unitId != message.unitId;
                playerRadioInfo.unitId = message.unitId;
            }


            if (overrideFreqAndVol)
            {
                playerRadioInfo.selected = message.selected;
                changed = true;
            }

            if (playerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT)
            {
                playerRadioInfo.selected = message.selected;
            }


            //copy over radio names, min + max
            for (var i = 0; i < playerRadioInfo.radios.Length; i++)
            {
                var clientRadio = playerRadioInfo.radios[i];

                //if awacs NOT open -  disable radios over 3
                if (i >= message.radios.Length
                    || (RadioOverlayWindow.AwacsActive == false
                        && (i > 3 || i == 0)
                        // disable intercom and all radios over 3 if awacs panel isnt open and we're a spectator given by the UnitId
                        && playerRadioInfo.unitId >= DCSPlayerRadioInfo.UnitIdOffset))
                {
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;

                    continue;
                }

                var updateRadio = message.radios[i];


                if ((updateRadio.expansion && !expansion) ||
                    (updateRadio.modulation == RadioInformation.Modulation.DISABLED))
                {
                    //expansion radio, not allowed
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;
                }
                else
                {
                    //update common parts
                    clientRadio.freqMin = updateRadio.freqMin;
                    clientRadio.freqMax = updateRadio.freqMax;

                    clientRadio.name = updateRadio.name;

                    clientRadio.modulation = updateRadio.modulation;

                    //update modes
                    clientRadio.freqMode = updateRadio.freqMode;

                    if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION))
                    {
                        clientRadio.encMode = updateRadio.encMode;
                    }
                    else
                    {
                        clientRadio.encMode = RadioInformation.EncryptionMode.NO_ENCRYPTION;
                    }

                    clientRadio.volMode = updateRadio.volMode;

                    if ((updateRadio.freqMode == RadioInformation.FreqMode.COCKPIT) || overrideFreqAndVol)
                    {
                        if (clientRadio.freq != updateRadio.freq)
                            changed = true;

                        if (clientRadio.secFreq != updateRadio.secFreq)
                            changed = true;

                        clientRadio.freq = updateRadio.freq;

                        //default overlay to off
                        if (updateRadio.freqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            clientRadio.secFreq = 0;
                        }
                        else
                        {
                            clientRadio.secFreq = updateRadio.secFreq;
                        }

                        clientRadio.channel = updateRadio.channel;
                    }
                    else
                    {
                        if (clientRadio.secFreq != 0)
                        {
                            //put back
                            clientRadio.secFreq = updateRadio.secFreq;
                        }

                        //check we're not over a limit
                        if (clientRadio.freq > clientRadio.freqMax)
                        {
                            clientRadio.freq = clientRadio.freqMax;
                        }
                        else if (clientRadio.freq < clientRadio.freqMin)
                        {
                            clientRadio.freq = clientRadio.freqMin;
                        }
                    }

                    //reset encryption
                    if (overrideFreqAndVol)
                    {
                        clientRadio.enc = false;
                        clientRadio.encKey = 0;
                    }

                    //Handle Encryption
                    if (updateRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY)
                    {
                        if (clientRadio.encKey == 0)
                        {
                            clientRadio.encKey = 1;
                        }
                    }
                    else if (clientRadio.encMode ==
                             RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE)
                    {
                        clientRadio.enc = updateRadio.enc;

                        if (clientRadio.encKey == 0)
                        {
                            clientRadio.encKey = 1;
                        }
                    }
                    else if (clientRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_FULL)
                    {
                        clientRadio.enc = updateRadio.enc;
                        clientRadio.encKey = updateRadio.encKey;
                    }
                    else
                    {
                        clientRadio.enc = false;
                        clientRadio.encKey = 0;
                    }

                    //handle volume
                    if ((updateRadio.volMode == RadioInformation.VolumeMode.COCKPIT) || overrideFreqAndVol)
                    {
                        clientRadio.volume = updateRadio.volume;
                    }

                    //handle Channels load for radios
                    if (newAircraft && i > 0)
                    {
                        if (clientRadio.freqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            var channelModel = _clientStateSingleton.FixedChannels[i - 1];
                            channelModel.Max = clientRadio.freqMax;
                            channelModel.Min = clientRadio.freqMin;
                            channelModel.Reload();
                            clientRadio.channel = -1; //reset channel

                            if (_settings.GetClientSetting(SettingsKeys.AutoSelectPresetChannel).BoolValue)
                            {
                                RadioHelper.RadioChannelUp(i);
                            }
                        }
                        else
                        {
                            _clientStateSingleton.FixedChannels[i - 1].Clear();
                            //clear
                        }
                    }
                }
            }

            //change PTT last
            if (!_settings.GetClientSetting(SettingsKeys.AllowDCSPTT).BoolValue)
            {
                playerRadioInfo.ptt =false;
            }
            else
            {
                playerRadioInfo.ptt = message.ptt;
            }
           
            //                }
            //            }

            //update
            playerRadioInfo.LastUpdate = DateTime.Now.Ticks;

            return changed;
        }

        private bool IsRadioInfoStale(DCSPlayerRadioInfo radioUpdate)
        {
            //send update if our metadata is nearly stale (1 tick = 100ns, 50000000 ticks = 5s stale timer)
            if (DateTime.Now.Ticks - _clientStateSingleton.LastSent < 50000000)
            {
                Logger.Debug($"Not Stale - Tick: {DateTime.Now.Ticks} Last sent: {_clientStateSingleton.LastSent} ");
                return false;
            }
            Logger.Debug($"Stale Radio - Tick: {DateTime.Now.Ticks} Last sent: {_clientStateSingleton.LastSent} ");
            return true;
        }

        public void Stop()
        {
            _stop = true;
            _stopExternalAWACSMode = true;
            IsListening = false;

            try
            {
                _dcsUdpListener?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _dcsGameGuiudpListener?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _dcsLOSListener?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _dcsRadioUpdateSender?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _udpCommandListener?.Close();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS
{
    public class DCSGameGuiHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DCSRadioSyncManager.ClientSideUpdate _clientSideUpdate;
        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private volatile bool _stop = false;
        private UdpClient _dcsGameGuiUdpListener;

        private ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public DCSGameGuiHandler(DCSRadioSyncManager.ClientSideUpdate clientSideUpdate)
        {
            _clientSideUpdate = clientSideUpdate;
        }

        public void Start()
        {
            _dcsGameGuiUdpListener = new UdpClient();
            _dcsGameGuiUdpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress,
                true);
            _dcsGameGuiUdpListener.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.

            var localEp = new IPEndPoint(IPAddress.Any,
                _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingGameGUIUDP));
            _dcsGameGuiUdpListener.Client.Bind(localEp);
            //   activeRadioUdpClient.Client.ReceiveTimeout = 10000;
            _clientStateSingleton.LastPostionCoalitionSent = 0;

            Task.Factory.StartNew(() =>
            {
                using (_dcsGameGuiUdpListener)
                {
                    //    var count = 0;
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any,
                            _globalSettings.GetNetworkSetting(GlobalSettingsKeys.DCSIncomingGameGUIUDP));
                            var bytes = _dcsGameGuiUdpListener.Receive(ref groupEp);

                            var updatedPlayerInfo =
                                JsonConvert.DeserializeObject<DCSPlayerSideInfo>(Encoding.UTF8.GetString(
                                    bytes, 0, bytes.Length));

                            if (updatedPlayerInfo != null)
                            {
                                var shouldUpdate = _serverSettings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) || _serverSettings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED);

                                var currentInfo = _clientStateSingleton.PlayerCoaltionLocationMetadata;

                                bool changed = !updatedPlayerInfo.name.Equals(currentInfo.name) || currentInfo.side!=updatedPlayerInfo.side;
                                //copy the bits we need  - leave position

                                currentInfo.name = updatedPlayerInfo.name;
                                currentInfo.side = updatedPlayerInfo.side;

                                //this will clear any stale positions if nothing is currently connected
                                _clientStateSingleton.ClearPositionsIfExpired();

                                //only update if position is changed 
                                if (_clientStateSingleton.DcsPlayerRadioInfo.IsCurrent() &&  (changed || shouldUpdate))
                                {
                                    _clientSideUpdate();
                                }
                                
                                //     count = 0;

                                _clientStateSingleton.DcsGameGuiLastReceived = DateTime.Now.Ticks;
                            }
                        }
                        catch (SocketException e)
                        {
                            // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling DCS GameGUI Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS GameGUI Message");
                        }
                    }

                    try
                    {
                        _dcsGameGuiUdpListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS listener ");
                    }
                }
            });
        }

        public void Stop()
        {
            _stop = true;
            try
            {
                _dcsGameGuiUdpListener?.Close();
            }
            catch (Exception ex)
            {
            }
        }
    }
}

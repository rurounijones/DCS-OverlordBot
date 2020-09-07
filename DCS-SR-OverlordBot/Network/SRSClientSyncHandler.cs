using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Easy.MessageHub;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class SrsClientSyncHandler
    {
        public delegate void ConnectCallback(bool result, bool connectionError, string connection);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private volatile bool _stop;

        public static string ServerVersion = "Unknown";
        private ConnectCallback _callback;
        private IPEndPoint _serverEndpoint;
        private TcpClient _tcpClient;

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;

        private DcsRadioSyncManager _radioDcsSync;

        private readonly string _guid = SettingsStore.Instance.GetClientSetting(SettingsKeys.CliendIdShort).StringValue;

        private const int MaxDecodeErrors = 5;

        #region Singleton Definition
        private static volatile SrsClientSyncHandler _instance;
        private static readonly object Lock = new object();

        public bool ApplicationStopped = false;

        public enum ConnectionState
        {
            Connected,
            Disconnected
        }

        public static SrsClientSyncHandler Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (Lock)
                {
                    if (_instance == null)
                        _instance = new SrsClientSyncHandler();
                }

                return _instance;
            }
        }
        #endregion


        private SrsClientSyncHandler()
        {
            // Appears not to work after a disconnect for some ungodly reason.
            //hub.Subscribe<ConnectionState>(cs => ProcessConnectionState(cs));
        }

        public void ProcessConnectionState(ConnectionState cs)
        {
            Logger.Debug($"Recieving Connection State {cs}");
            switch (cs)
            {
                case ConnectionState.Connected:
                    ConnectExternalAwacsMode();
                    break;
                case ConnectionState.Disconnected:
                    DisconnectExternalAwacsMode();
                    break;
            }
        }

        public void TryConnect(IPEndPoint endpoint, ConnectCallback callback)
        {
            _callback = callback;
            _serverEndpoint = endpoint;

            var tcpThread = new Thread(Connect);
            tcpThread.Start();
        }

        public void ConnectExternalAwacsMode()
        {
            if (_clientStateSingleton.ExternalAwacsModeConnected)
            {
                return;
            }

            _clientStateSingleton.ExternalAwacsModeSelected = true;

            var sideInfo = _clientStateSingleton.PlayerCoalitionLocationMetadata;
            sideInfo.name = _clientStateSingleton.LastSeenName;
            SendToServer(new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = sideInfo.side,
                    Name = sideInfo.name,
                    LatLngPosition = sideInfo.LngLngPosition,
                    ClientGuid = _guid
                },
                ExternalAWACSModePassword = ClientStateSingleton.Instance.ExternalAwacsModePassword,
                MsgType = NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD
            });
        }

        public void DisconnectExternalAwacsMode()
        {
            _radioDcsSync.StopExternalAwacsModeLoop();
            CallExternalAWACSModeOnMain(false, 0);
        }

        private void Connect()
        {

            if (_radioDcsSync != null)
            {
                _radioDcsSync.Stop();
                _radioDcsSync = null;
            }

            var connectionError = false;

            _radioDcsSync = new DcsRadioSyncManager(ClientRadioUpdated);

            using (_tcpClient = new TcpClient())
            {
                try
                {
                    _tcpClient.SendTimeout = 10000;
                    _tcpClient.NoDelay = true;

                    // Wait for 10 seconds before aborting connection attempt - no SRS server running/port opened in that case
                    _tcpClient.ConnectAsync(_serverEndpoint.Address, _serverEndpoint.Port).Wait(TimeSpan.FromSeconds(10));

                    if (_tcpClient.Connected)
                    {
                        _tcpClient.NoDelay = true;

                        CallOnMain(true);
                        ClientSyncLoop();
                    }
                    else
                    {
                        Logger.Error($"Failed to connect to server @ {_serverEndpoint}");

                        // Signal disconnect including an error
                        connectionError = true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Could not connect to server");
                    connectionError = true;
                }
            }
            //disconnect callback
            CallOnMain(false, connectionError);
        }

        private void ClientRadioUpdated()
        {
            Logger.Debug("Sending Radio Update to Server");
            var sideInfo = _clientStateSingleton.PlayerCoalitionLocationMetadata;
            SendToServer(new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = sideInfo.side,
                    Name = sideInfo.name,
                    ClientGuid = _guid,
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                    LatLngPosition = sideInfo.LngLngPosition
                },
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE
            });
        }

        private void CallOnMain(bool result, bool connectionError = false)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                    new ThreadStart(delegate { _callback(result, connectionError, _serverEndpoint.ToString()); }));
            }
            catch (Exception ex)
            {
                 Logger.Error(ex, "Failed to update UI after connection callback (result {result}, connectionError {connectionError})", result, connectionError);
            }
        }

        private void CallExternalAWACSModeOnMain(bool result, int coalition)
        {
            _clientStateSingleton.ExternalAwacsModeSelected = result;

            if (result)
            {
                _clientStateSingleton.PlayerCoalitionLocationMetadata.side = coalition;
                _clientStateSingleton.PlayerCoalitionLocationMetadata.name = _clientStateSingleton.LastSeenName;
                _clientStateSingleton.DcsPlayerRadioInfo.name = _clientStateSingleton.LastSeenName;
            }
            else
            {
                _clientStateSingleton.PlayerCoalitionLocationMetadata.side = 0;
                _clientStateSingleton.PlayerCoalitionLocationMetadata.name = "";
                _clientStateSingleton.DcsPlayerRadioInfo.name = "";
                _clientStateSingleton.DcsPlayerRadioInfo.LastUpdate = 0;
                _clientStateSingleton.LastSent = 0;
            }
        }

        private void ClientSyncLoop()
        {
            //clear the clients list
            _clients.Clear();
            var decodeErrors = 0; //if the JSON is unreadable - new version likely

            using (var reader = new StreamReader(_tcpClient.GetStream(), Encoding.UTF8))
            {
                try
                {
                    var sideInfo = _clientStateSingleton.PlayerCoalitionLocationMetadata;
                    //start the loop off by sending a SYNC Request
                    SendToServer(new NetworkMessage
                    {
                        Client = new SRClient
                        {
                            Coalition = sideInfo.side,
                            Name = sideInfo.name.Length > 0 ? sideInfo.name : _clientStateSingleton.LastSeenName,
                            LatLngPosition = sideInfo.LngLngPosition,
                            ClientGuid = _guid
                        },
                        MsgType = NetworkMessage.MessageType.SYNC
                    });

                    string line;
                    while ((line = reader.ReadLine()) != null && ApplicationStopped == false)
                    {
                        try
                        {
                            var serverMessage = JsonConvert.DeserializeObject<NetworkMessage>(line);
                            decodeErrors = 0; //reset counter
                            if (serverMessage != null)
                            {
                                //Logger.Debug("Received "+serverMessage.MsgType);
                                switch (serverMessage.MsgType)
                                {
                                    case NetworkMessage.MessageType.PING:
                                        // Do nothing for now
                                        break;
                                    case NetworkMessage.MessageType.RADIO_UPDATE:
                                    case NetworkMessage.MessageType.UPDATE:

                                        if (serverMessage.ServerSettings != null)
                                        {
                                            _serverSettings.Decode(serverMessage.ServerSettings);
                                        }

                                        if (_clients.ContainsKey(serverMessage.Client.ClientGuid))
                                        {
                                            var srClient = _clients[serverMessage.Client.ClientGuid];
                                            var updatedSrClient = serverMessage.Client;
                                            if (srClient != null)
                                            {
                                                srClient.LastUpdate = DateTime.Now.Ticks;
                                                srClient.Name = updatedSrClient.Name;
                                                srClient.Coalition = updatedSrClient.Coalition;

                                                srClient.LatLngPosition = updatedSrClient.LatLngPosition;

                                                if (updatedSrClient.RadioInfo != null)
                                                {
                                                    srClient.RadioInfo = updatedSrClient.RadioInfo;
                                                    srClient.RadioInfo.LastUpdate = DateTime.Now.Ticks;
                                                }
                                                else
                                                {
                                                    //radio update but null RadioInfo means no change
                                                    if (serverMessage.MsgType ==
                                                        NetworkMessage.MessageType.RADIO_UPDATE &&
                                                        srClient.RadioInfo != null)
                                                    {
                                                        srClient.RadioInfo.LastUpdate = DateTime.Now.Ticks;
                                                    }
                                                }

                                                // Logger.Debug("Received Update Client: " + NetworkMessage.MessageType.UPDATE + " From: " +
                                                //             srClient.Name + " Coalition: " +
                                                //             srClient.Coalition + " Pos: " + srClient.LatLngPosition);
                                            }
                                        }
                                        else
                                        {
                                            var connectedClient = serverMessage.Client;
                                            connectedClient.LastUpdate = DateTime.Now.Ticks;

                                            //init with LOS true so you can hear them incase of bad DCS install where
                                            //LOS isnt working
                                            connectedClient.LineOfSightLoss = 0.0f;
                                            //0.0 is NO LOSS therefore full Line of sight

                                            _clients[serverMessage.Client.ClientGuid] = connectedClient;

                                            // Logger.Debug("Received New Client: " + NetworkMessage.MessageType.UPDATE +
                                            //             " From: " +
                                            //             serverMessage.Client.Name + " Coalition: " +
                                            //             serverMessage.Client.Coalition);
                                        }

                                        if (_clientStateSingleton.ExternalAwacsModeSelected &&
                                            !_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                        {
                                            DisconnectExternalAwacsMode();
                                        }

                                        break;
                                    case NetworkMessage.MessageType.SYNC:
                                        // Logger.Info("Recevied: " + NetworkMessage.MessageType.SYNC);

                                        //check server version
                                        if (serverMessage.Version == null)
                                        {
                                            Logger.Error("Disconnecting Unversioned Server");
                                            Disconnect();
                                            break;
                                        }

                                        var serverVersion = Version.Parse(serverMessage.Version);
                                        var protocolVersion = Version.Parse(UpdaterChecker.MINIMUM_PROTOCOL_VERSION);

                                        ServerVersion = serverMessage.Version;

                                        if (serverVersion < protocolVersion)
                                        {
                                            Logger.Error($"Server version ({serverMessage.Version}) older than minimum procotol version ({UpdaterChecker.MINIMUM_PROTOCOL_VERSION}) - disconnecting");

                                            ShowVersionMistmatchWarning(serverMessage.Version);

                                            Disconnect();
                                            break;
                                        }

                                        if (serverMessage.Clients != null)
                                        {
                                            foreach (var client in serverMessage.Clients)
                                            {
                                                client.LastUpdate = DateTime.Now.Ticks;
                                                //init with LOS true so you can hear them incase of bad DCS install where
                                                //LOS isnt working
                                                client.LineOfSightLoss = 0.0f;
                                                //0.0 is NO LOSS therefore full Line of sight
                                                _clients[client.ClientGuid] = client;
                                            }
                                        }
                                        //add server settings
                                        _serverSettings.Decode(serverMessage.ServerSettings);

                                        if (_clientStateSingleton.ExternalAwacsModeSelected &&
                                            !_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                        {
                                            DisconnectExternalAwacsMode();
                                        }

                                        break;

                                    case NetworkMessage.MessageType.SERVER_SETTINGS:

                                        _serverSettings.Decode(serverMessage.ServerSettings);
                                        ServerVersion = serverMessage.Version;

                                        if (_clientStateSingleton.ExternalAwacsModeSelected &&
                                            !_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                                        {
                                            DisconnectExternalAwacsMode();
                                        }
                                        break;
                                    case NetworkMessage.MessageType.CLIENT_DISCONNECT:

                                        _clients.TryRemove(serverMessage.Client.ClientGuid, out var outClient);

                                        if (outClient != null)
                                        {
                                            MessageHub.Instance.Publish(outClient);
                                        }

                                        break;
                                    case NetworkMessage.MessageType.VERSION_MISMATCH:
                                        Logger.Error($"Version Mismatch Between Client ({UpdaterChecker.VERSION}) & Server ({serverMessage.Version}) - Disconnecting");

                                        ShowVersionMistmatchWarning(serverMessage.Version);

                                        Disconnect();
                                        break;
                                    case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD:
                                        if (serverMessage.Client.Coalition == 0)
                                        {
                                            Logger.Info("External AWACS mode authentication failed");

                                            CallExternalAWACSModeOnMain(false, 0);
                                        }
                                        else if (_radioDcsSync != null)
                                        {
                                            Logger.Info("External AWACS mode authentication succeeded, coalition {0}", serverMessage.Client.Coalition == 1 ? "red" : "blue");

                                            CallExternalAWACSModeOnMain(true, serverMessage.Client.Coalition);

                                            _radioDcsSync.StartExternalAwacsModeLoop();
                                        }
                                        break;
                                    default:
                                        Logger.Error("Recevied unknown " + line);
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"Error decoding message from server: {line}");
                            decodeErrors++;
                            if (!_stop)
                            {
                                Logger.Error(ex, "Client exception reading from socket ");
                            }

                            if (decodeErrors <= MaxDecodeErrors) continue;
                            Logger.Error("Too many errors decoding server messagse. disconnecting");
                            Disconnect();
                            break;
                        }

                        // do something with line
                    }
                }
                catch (Exception ex)
                {
                    if (!_stop)
                    {
                        Logger.Error(ex, "Client exception reading - Disconnecting ");
                    }
                }
            }

            //disconnected - reset DCS Info
            ClientStateSingleton.Instance.DcsPlayerRadioInfo.LastUpdate = 0;

            //clear the clients list
            _clients.Clear();

            Disconnect();
        }

        private static void ShowVersionMistmatchWarning(string serverVersion)
        {
            MessageBox.Show("The SRS server you're connecting to is incompatible with this Client. " +
                            "\n\nMake sure to always run the latest version of the SRS Server & Client" +
                            $"\n\nServer Version: {serverVersion}" +
                            $"\nClient Version: {UpdaterChecker.VERSION}" +
                            $"\nMinimum Version: {UpdaterChecker.MINIMUM_PROTOCOL_VERSION}",
                            "SRS Server Incompatible",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
        }

        private void SendToServer(NetworkMessage message)
        {
            try
            {

                message.Version = UpdaterChecker.VERSION;

                var json = message.Encode();

                if (message.MsgType == NetworkMessage.MessageType.RADIO_UPDATE)
                {
                    Logger.Debug("Sending Radio Update To Server: "+ json);
                }

                var bytes = Encoding.UTF8.GetBytes(json);
                try
                {
                    _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
                    _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
                } catch (ObjectDisposedException ex)
                {
                    Logger.Debug(ex, $"Tried writing message type {message.MsgType} to a disposed TcpClient");
                }
                //Need to flush?
            }
            catch (Exception ex)
            {
                if (!_stop)
                {
                    Logger.Error(ex, $"Client exception sending message type {message.MsgType} to server");
                }

                Disconnect();
            }
        }

        //implement IDispose? To close stuff properly?
        public void Disconnect()
        {
            _stop = true;

            DisconnectExternalAwacsMode();

            _tcpClient?.Close(); // this'll stop the socket blocking

            Logger.Error("Disconnecting from server");
            ClientStateSingleton.Instance.IsConnected = false;

            //CallOnMain(false);
        }
    }
}
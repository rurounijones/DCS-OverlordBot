using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using RurouniJones.DCS.OverlordBot.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Easy.MessageHub;
using Newtonsoft.Json;
using NLog;

namespace RurouniJones.DCS.OverlordBot.Network
{
    public class SrsDataClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static string ServerVersion = "Unknown";
        private IPEndPoint _serverEndpoint;
        private TcpClient _tcpClient;

        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
        private readonly Client _mainClient;

        private readonly string _guid;

        private const int MaxDecodeErrors = 5;

        private volatile bool _requestStop;

        public SrsDataClient(Client mainClient)
        {
            _mainClient = mainClient;
            _guid = mainClient.ShortGuid;
        }

        public void ConnectExternalAwacsMode()
        {
            if (_mainClient.ExternalAwacsModeConnected)
            {
                return;
            }

            _mainClient.ExternalAwacsModeSelected = true;

            var sideInfo = _mainClient.PlayerCoalitionLocationMetadata;
            sideInfo.name = _mainClient.LastSeenName;

            var message = new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = sideInfo.side,
                    Name = sideInfo.name,
                    LatLngPosition = sideInfo.LngLngPosition,
                    ClientGuid = _guid
                },
                ExternalAWACSModePassword = _mainClient.ExternalAwacsModePassword,
                MsgType = NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD
            };

            if (SendToServer(message)) return;
            Logger.Error("Error connecting to external AWACS mode");
            _requestStop = true;
        }

        public bool Connect(IPEndPoint endpoint)
        {
            _serverEndpoint = endpoint;

            _requestStop = false;
            _tcpClient = new TcpClient();

            try
            {
                _tcpClient.SendTimeout = 10000;
                _tcpClient.NoDelay = true;

                // Wait for 10 seconds before aborting connection attempt - no SRS server running/port opened in that case
                _tcpClient.ConnectAsync(_serverEndpoint.Address, _serverEndpoint.Port).Wait(TimeSpan.FromSeconds(10));

                if (_tcpClient.Connected)
                {
                    _tcpClient.NoDelay = true;
                    return true;
                }

                Logger.Error($"{_mainClient.LogClientId}| Failed to connect to server @ {_serverEndpoint}");
                return false;

            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{_mainClient.LogClientId}| Could not connect to server");
                return false;
            }

        }

        private void SendAwacsRadioInformation()
        {
            _mainClient.LastSent = 0;
            _mainClient.ExternalAwacsModeConnected = true;

            var sideInfo = _mainClient.PlayerCoalitionLocationMetadata;

            var message = new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = sideInfo.side,
                    Name = sideInfo.name,
                    ClientGuid = _guid,
                    RadioInfo = _mainClient.DcsPlayerRadioInfo,
                    LatLngPosition = sideInfo.LngLngPosition
                },
                MsgType = NetworkMessage.MessageType.RADIO_UPDATE
            };

            SendToServer(message);
        }

        public void ClientSyncLoop()
        {
            //clear the clients list
            _mainClient.Clear();
            var decodeErrors = 0; //if the JSON is unreadable - new version likely

            using (var reader = new StreamReader(_tcpClient.GetStream(), Encoding.UTF8))
            {
                try
                {
                    var sideInfo = _mainClient.PlayerCoalitionLocationMetadata;
                    //start the loop off by sending a SYNC Request
                    var success = SendToServer(new NetworkMessage
                    {
                        Client = new SRClient
                        {
                            Coalition = sideInfo.side,
                            Name = sideInfo.name.Length > 0 ? sideInfo.name : _mainClient.LastSeenName,
                            LatLngPosition = sideInfo.LngLngPosition,
                            ClientGuid = _guid
                        },
                        MsgType = NetworkMessage.MessageType.SYNC
                    });

                    if (!success)
                    {
                        Logger.Error($"{_mainClient.LogClientId}| Error sending initial SYNC");
                        _mainClient.IsDataConnected = false;
                        return;
                    }

                    string line;
                    while (_requestStop == false && (line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var serverMessage = JsonConvert.DeserializeObject<NetworkMessage>(line);
                            decodeErrors = 0; //reset counter
                            if (serverMessage != null)
                            {
                                Logger.Trace($"{_mainClient.LogClientId}| Message {serverMessage.MsgType} received: {line}"); 
                                if(!ProcessMessage(serverMessage, line))
                                {
                                    break;
                                }
                                
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"{_mainClient.LogClientId}| Error decoding message from server: {line}");
                            decodeErrors++;
                            Logger.Error(ex, $"{_mainClient.LogClientId}| Client exception reading from socket ");

                            if (decodeErrors <= MaxDecodeErrors) continue;
                            Logger.Error($"{_mainClient.LogClientId}| Too many errors decoding server messages. Aborting Connection");
                            break;
                        }
                    }
                    Logger.Debug($"{_mainClient.LogClientId}| Stopping ClientSyncLoop. RequestStop is {_requestStop}");
                }
                catch (Exception ex)
                {
                        Logger.Error(ex, $"{_mainClient.LogClientId}| Client exception reading ");
                }
            }
            _mainClient.IsAudioConnected = false;
        }

        private bool ProcessMessage(NetworkMessage serverMessage, string line)
        {
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

                    if (_mainClient.ContainsKey(serverMessage.Client.ClientGuid))
                    {
                        var srClient = _mainClient[serverMessage.Client.ClientGuid];
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
                                srClient.RadioInfo.inAircraft = updatedSrClient.RadioInfo.inAircraft;
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

                        _mainClient[serverMessage.Client.ClientGuid] = connectedClient;
                    }

                    if (_mainClient.ExternalAwacsModeSelected &&
                        !_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                    {
                        Logger.Error($"{_mainClient.LogClientId}| This server does not support External Awacs mode");
                        return false;
                    }

                    break;
                case NetworkMessage.MessageType.SYNC:
                    //check server version
                    if (serverMessage.Version == null)
                    {
                        Logger.Error($"{_mainClient.LogClientId}| Unversioned Server - Aborting Connection");
                        return false;
                    }

                    var serverVersion = Version.Parse(serverMessage.Version);
                    var protocolVersion = Version.Parse(UpdaterChecker.MINIMUM_PROTOCOL_VERSION);

                    ServerVersion = serverMessage.Version;

                    if (serverVersion < protocolVersion)
                    {
                        Logger.Error($"{_mainClient.LogClientId}| Server version ({serverMessage.Version}) older than minimum" +
                                     "procotol version ({UpdaterChecker.MINIMUM_PROTOCOL_VERSION}) - Aborting Connection");

                        return false;
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
                            _mainClient[client.ClientGuid] = client;
                        }
                    }

                    //add server settings
                    _serverSettings.Decode(serverMessage.ServerSettings);

                    if (_mainClient.ExternalAwacsModeSelected &&
                        !_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                    {
                        Logger.Error($"{_mainClient.LogClientId}| This server does not support External Awacs mode - Aborting Connection");
                        return false;
                    }

                    break;

                case NetworkMessage.MessageType.SERVER_SETTINGS:

                    _serverSettings.Decode(serverMessage.ServerSettings);
                    ServerVersion = serverMessage.Version;

                    if (_mainClient.ExternalAwacsModeSelected &&
                        !_serverSettings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE))
                    {
                        Logger.Error($"{_mainClient.LogClientId}| This server does not support External Awacs mode - Aborting Connection");
                        return false;
                    }

                    break;
                case NetworkMessage.MessageType.CLIENT_DISCONNECT:

                    _mainClient.TryRemove(serverMessage.Client.ClientGuid, out var outClient);

                    if (outClient != null)
                    {
                        MessageHub.Instance.Publish(outClient);
                    }
                    break;
                case NetworkMessage.MessageType.VERSION_MISMATCH:
                    Logger.Error(
                        $"{_mainClient.LogClientId}| Version Mismatch Between Client ({UpdaterChecker.VERSION}) & Server ({serverMessage.Version}) - Aborting Connection");
                    return false;
                case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD:
                    if (serverMessage.Client.Coalition > 0)
                    {
                        Logger.Info($"{_mainClient.LogClientId}| External AWACS mode authentication succeeded, coalition {0}",
                            serverMessage.Client.Coalition == 1 ? "red" : "blue");
                        _mainClient.PlayerCoalitionLocationMetadata.side = serverMessage.Client.Coalition;
                        _mainClient.PlayerCoalitionLocationMetadata.name = _mainClient.LastSeenName;
                        _mainClient.DcsPlayerRadioInfo.name = _mainClient.LastSeenName;
                        SendAwacsRadioInformation();
                    }
                    else
                    {
                        Logger.Info($"{_mainClient.LogClientId}| External AWACS mode authentication failed");
                        return false;
                    }

                    break;
                default:
                    Logger.Error($"{_mainClient.LogClientId}| Received unknown " + line);
                    break;
            }

            return true;
        }

        private bool SendToServer(NetworkMessage message)
        {
            try
            {

                message.Version = UpdaterChecker.VERSION;

                var json = message.Encode();

                var bytes = Encoding.UTF8.GetBytes(json);
                try
                {
                    _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
                    Logger.Trace($"{_mainClient.LogClientId}| Message {message.MsgType} sent: {json}");
                    return true;

                } catch (ObjectDisposedException ex)
                {
                    Logger.Debug(ex, $"{_mainClient.LogClientId}| Tried writing message type {message.MsgType} to a disposed TcpClient");
                    return false;
                }
                //Need to flush?
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{_mainClient.LogClientId}| Client exception sending message type {message.MsgType} to server");
                return false;
            }
        }

        //implement IDispose? To close stuff properly?
        public void RequestStop()
        {
            _requestStop = true;

            _tcpClient?.Dispose(); // this'll stop the socket blocking
            _tcpClient = null;

            Logger.Error($"{_mainClient.LogClientId}| Disconnecting data connection from server");
        }
    }
}
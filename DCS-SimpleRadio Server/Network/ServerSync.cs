using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using Newtonsoft.Json;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    // State object for reading client data asynchronously
    public class StateObject
    {
        // Size of receive buffer.
        public const int BufferSize = 1024;

        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];

        public string guid;

        // Received data string.
        public StringBuilder sb = new StringBuilder();

        // Client  socket.
        public Socket workSocket;
    }

    internal class ServerSync : IHandle<ServerSettingsChangedMessage>
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        // Thread signal.
        public static ManualResetEvent _allDone = new ManualResetEvent(false);

        private readonly HashSet<IPAddress> _bannedIps;

        private readonly ConcurrentDictionary<string, SRClient> _clients = new ConcurrentDictionary<string, SRClient>();
        private readonly IEventAggregator _eventAggregator;

        private readonly ServerSettingsStore _serverSettings;

        private Socket listener;

        public ServerSync(ConcurrentDictionary<string, SRClient> connectedClients, HashSet<IPAddress> _bannedIps,
            IEventAggregator eventAggregator)
        {
            _clients = connectedClients;
            this._bannedIps = _bannedIps;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            _serverSettings = ServerSettingsStore.Instance;
        }

        public void Handle(ServerSettingsChangedMessage message)
        {
            foreach (var clientToSent in _clients)
            {
                try
                {
                    HandleServerSettingsMessage(clientToSent.Value.ClientSocket);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Exception Sending Server Settings ");
                }
            }
        }

        public void StartListening()
        {
            var ipAddress = new IPAddress(0);
            var port = _serverSettings.GetServerPort();
            var localEndPoint = new IPEndPoint(ipAddress, port);

            // Create a TCP/IP socket.
            listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            listener.NoDelay = true;

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
                listener.NoDelay = true;
                while (true)
                {
                    // Set the event to nonsignaled state.
                    _allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    _logger.Info($"Waiting for a connection on {port}...");
                    listener.BeginAccept(
                        AcceptCallback,
                        listener);

                    // Wait until a connection is made before continuing.
                    _allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Server Listen error: " + e.Message);
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.
            _allDone.Set();

            try
            {
                // Get the socket that handles the client request.
                var listener = (Socket) ar.AsyncState;
                var handler = listener.EndAccept(ar);
                handler.NoDelay = true;

                // Create the state object.
                var state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    ReadCallback, state);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error accepting socket");
            }
        }

        public void HandleDisconnect(StateObject state)
        {
            _logger.Info("Disconnecting Client");

            if ((state != null) && (state.guid != null))
            {
                //removed
                SRClient client;
                _clients.TryRemove(state.guid, out client);

                if (client != null)
                {
                    HandleClientDisconnect(client);

                    _logger.Info("Removed Client " + state.guid);
                }

                try
                {
                    _eventAggregator.PublishOnUIThread(
                        new ServerStateMessage(true, new List<SRClient>(_clients.Values)));
                }
                catch (Exception ex)
                {
                    _logger.Info(ex, "Exception Publishing Client Update After Disconnect");
                }
            }

            try
            {
                state.workSocket.Close();
            }
            catch (Exception ex)
            {
                _logger.Info(ex, "Exception closing socket after disconnect");
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket
            // from the asynchronous state object.
            var state = (StateObject) ar.AsyncState;
            var handler = state.workSocket;

            try
            {
                // Read data from the client socket.
                var bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.
                    state.sb.Append(Encoding.UTF8.GetString(
                        state.buffer, 0, bytesRead));

                    List<string> messages = GetNetworkMessage(state.sb);

                    if (messages.Count > 0)
                    {
                        foreach (var content in messages)
                        {
                            //Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
                            //   content.Length, content);

                            try
                            {
                                var message = JsonConvert.DeserializeObject<NetworkMessage>(content);

                                HandleMessage(state, message);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, "Server - Client Exception reading");
                            }
                        }
                    }

                    //continue receiving more
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        ReadCallback, state);
                }
                else
                {
                    HandleDisconnect(state);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading from socket. Disconnecting ");

                HandleDisconnect(state);
            }
        }

        private List<string> GetNetworkMessage(StringBuilder buf)
        {
            List<string> messages = new List<string>();
            //search for a \n, extract up to that \n and then remove from buffer
            var content = buf.ToString();
            while (content.Length > 2 && content.Contains("\n"))
            {
                //extract message
                var message = content.Substring(0, content.IndexOf("\n") + 1);

                //now clear from buffer
                buf.Remove(0, message.Length);

                //trim the received part
                messages.Add(message.Trim());

                //load in next part
                content = buf.ToString();
            }

            return messages;
        }

        public void HandleMessage(StateObject state, NetworkMessage message)
        {
            try
            {
                var clientIp = (IPEndPoint) state.workSocket.RemoteEndPoint;

                if (_bannedIps.Contains(clientIp.Address))
                {
                    state.workSocket.Disconnect(true);

                    _logger.Warn("Disconnecting Banned Client -  " + clientIp.Address + " " + clientIp.Port);
                    return;
                }

                //  logger.Info("Received From " + clientIp.Address + " " + clientIp.Port);
                // logger.Info("Recevied: " + message.MsgType);

                switch (message.MsgType)
                {
                    case NetworkMessage.MessageType.PING:
                        // Do nothing for now
                        break;
                    case NetworkMessage.MessageType.UPDATE:
                        HandleClientMetaDataUpdate(message);
                        break;
                    case NetworkMessage.MessageType.RADIO_UPDATE:
                        HandleClientMetaDataUpdate(message);
                        HandleClientRadioUpdate(message);
                        break;
                    case NetworkMessage.MessageType.SYNC:

                        var srClient = message.Client;
                        if (!_clients.ContainsKey(srClient.ClientGuid))
                        {
                            if (message.Version == null)
                            {
                                _logger.Warn("Disconnecting Unversioned Client -  " + clientIp.Address + " " +
                                             clientIp.Port);
                                state.workSocket.Disconnect(true);
                                return;
                            }

                            var clientVersion = Version.Parse(message.Version);
                            var protocolVersion = Version.Parse(UpdaterChecker.MINIMUM_PROTOCOL_VERSION);

                            if (clientVersion < protocolVersion)
                            {
                                _logger.Warn(
                                    $"Disconnecting Unsupported  Client Version - Version {clientVersion} IP {clientIp.Address} Port {clientIp.Port}");
                                HandleVersionMismatch(state.workSocket);

                                //close socket after
                                state.workSocket.Disconnect(true);

                                return;
                            }

                            srClient.ClientSocket = state.workSocket;

                            //add to proper list
                            _clients[srClient.ClientGuid] = srClient;

                            state.guid = srClient.ClientGuid;

                            _eventAggregator.PublishOnUIThread(new ServerStateMessage(true,
                                new List<SRClient>(_clients.Values)));
                        }

                        HandleRadioClientsSync(clientIp, state.workSocket, message);

                        break;
                    case NetworkMessage.MessageType.SERVER_SETTINGS:
                        HandleServerSettingsMessage(state.workSocket);
                        break;
                    case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD:
                        HandleExternalAWACSModePassword(state.workSocket, message.ExternalAWACSModePassword, message.Client);
                        break;
                    case NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_DISCONNECT:
                        HandleExternalAWACSModeDisconnect(message.Client);
                        break;
                    default:
                        _logger.Warn("Recevied unknown message type");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception Handling Message " + ex.Message);
            }
        }

        private void HandleServerSettingsMessage(Socket socket)
        {
            //send server settings
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.SERVER_SETTINGS,
                ServerSettings = _serverSettings.ToDictionary()
            };
            Send(socket, replyMessage);
        }

        private void HandleVersionMismatch(Socket socket)
        {
            //send server settings
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.VERSION_MISMATCH,
            };
            Send(socket, replyMessage);
        }

        private void HandleClientMetaDataUpdate(NetworkMessage message)
        {
            if (_clients.ContainsKey(message.Client.ClientGuid))
            {
                var client = _clients[message.Client.ClientGuid];

                if (client != null)
                {
                    bool redrawClientAdminList = client.Name != message.Client.Name || client.Coalition != message.Client.Coalition;

                    //copy the data we need
                    client.LastUpdate = DateTime.Now.Ticks;
                    client.Name = message.Client.Name;
                    client.Coalition = message.Client.Coalition;
                    client.Position = message.Client.Position;


                    //send update to everyone
                    //Remove Client Radio Info
                    var replyMessage = new NetworkMessage
                    {
                        MsgType = NetworkMessage.MessageType.UPDATE,
                        ServerSettings = _serverSettings.ToDictionary(),
                        Client = new SRClient
                        {
                            ClientGuid = client.ClientGuid,
                            Coalition = client.Coalition,
                            Name = client.Name,
                            LastUpdate = client.LastUpdate,
                            Position = client.Position
                        }
                    };

                    foreach (var clientToSent in _clients)
                    {
                        Send(clientToSent.Value.ClientSocket, replyMessage);
                    }

                    // Only redraw client admin UI of server if really needed
                    if (redrawClientAdminList)
                    {
                        _eventAggregator.PublishOnUIThread(new ServerStateMessage(true,
                            new List<SRClient>(_clients.Values)));
                    }
                }
            }
        }

        private void HandleClientDisconnect(SRClient client)
        {
            var message = new NetworkMessage()
            {
                Client = client,
                MsgType = NetworkMessage.MessageType.CLIENT_DISCONNECT
            };

            foreach (var clientToSent in _clients)
            {
                Send(clientToSent.Value.ClientSocket, message);
            }
        }

        private void HandleClientRadioUpdate(NetworkMessage message)
        {
            if (_clients.ContainsKey(message.Client.ClientGuid))
            {
                var client = _clients[message.Client.ClientGuid];

                if (client != null)
                {
                    //update to local ticks
                    message.Client.RadioInfo.LastUpdate = DateTime.Now.Ticks;

                    client.LastUpdate = DateTime.Now.Ticks;
                    client.Name = message.Client.Name;
                    client.Coalition = message.Client.Coalition;
                    client.RadioInfo = message.Client.RadioInfo;
                    client.Position = message.Client.Position;


                    //    _logger.Info("Received Radio Update");
                }
            }
        }

        private void HandleRadioClientsSync(IPEndPoint clientIp, Socket clientSocket, NetworkMessage message)
        {
            //store new client
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.SYNC,
                Clients = new List<SRClient>(),
                ServerSettings = _serverSettings.ToDictionary()
            };

            foreach (var clientToSent in _clients)
            {
                replyMessage.Clients.Add(clientToSent.Value);
            }

            Send(clientSocket, replyMessage);
        }

        private void HandleExternalAWACSModePassword(Socket clientSocket, string password, SRClient client)
        {
            // Response of clientCoalition = 0 indicates authentication success (or external AWACS mode disabled)
            int clientCoalition = 0;
            if (_serverSettings.GetGeneralSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE).BoolValue
                && !string.IsNullOrWhiteSpace(password))
            {
                if (_serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_BLUE_PASSWORD).StringValue == password)
                {
                    clientCoalition = 2;
                }
                else if (_serverSettings.GetExternalAWACSModeSetting(ServerSettingsKeys.EXTERNAL_AWACS_MODE_RED_PASSWORD).StringValue == password)
                {
                    clientCoalition = 1;
                }
            }

            if (_clients.ContainsKey(client.ClientGuid))
            {
                _clients[client.ClientGuid].Coalition = clientCoalition;
                _clients[client.ClientGuid].Name = client.Name;

                _eventAggregator.PublishOnUIThread(new ServerStateMessage(true,
                    new List<SRClient>(_clients.Values)));
            }

            var replyMessage = new NetworkMessage
            {
                Client = new SRClient
                {
                    Coalition = clientCoalition
                },
                MsgType = NetworkMessage.MessageType.EXTERNAL_AWACS_MODE_PASSWORD,
            };
            Send(clientSocket, replyMessage);

            var message = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.UPDATE,
                ServerSettings = _serverSettings.ToDictionary(),
                Client = new SRClient
                {
                    ClientGuid = client.ClientGuid,
                    Coalition = clientCoalition,
                    Name = client.Name,
                    LastUpdate = client.LastUpdate,
                    Position = client.Position
                }
            };

            foreach (var clientToSent in _clients)
            {
                Send(clientToSent.Value.ClientSocket, message);
            }
        }

        private void HandleExternalAWACSModeDisconnect(SRClient client)
        {
            if (_clients.ContainsKey(client.ClientGuid))
            {
                _clients[client.ClientGuid].Coalition = 0;
                _clients[client.ClientGuid].Name = "";

                _eventAggregator.PublishOnUIThread(new ServerStateMessage(true,
                    new List<SRClient>(_clients.Values)));

                var message = new NetworkMessage
                {
                    MsgType = NetworkMessage.MessageType.UPDATE,
                    ServerSettings = _serverSettings.ToDictionary(),
                    Client = new SRClient
                    {
                        ClientGuid = client.ClientGuid,
                        Coalition = client.Coalition,
                        Name = client.Name,
                        LastUpdate = client.LastUpdate,
                        Position = client.Position
                    }
                };

                foreach (var clientToSent in _clients)
                {
                    Send(clientToSent.Value.ClientSocket, message);
                }
            }
        }

        private static void Send(Socket handler, NetworkMessage message)
        {
            try
            {
                message.Version = UpdaterChecker.VERSION;

                // Convert the string data to byte data using ASCII encoding.
                var byteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message) + "\n");

                // Begin sending the data to the remote device.
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    SendCallback, handler);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception Sending Message " + ex.Message);
            }
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                var handler = (Socket) ar.AsyncState;

                // Complete sending the data to the remote device.
                var bytesSent = handler.EndSend(ar);
                //  Console.WriteLine("Sent {0} bytes to client.", bytesSent);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Exception SendCallback ");
            }
        }

        public void RequestStop()
        {
            try
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        client.Value.ClientSocket.Close();
                    }
                    catch (Exception ex)
                    {
                    }
                }
                listener.Close();

                _clients.Clear();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
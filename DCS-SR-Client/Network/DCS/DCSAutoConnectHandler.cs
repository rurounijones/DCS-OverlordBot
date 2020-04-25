using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS
{
    public class DCSAutoConnectHandler
    {
        private readonly MainWindow.ReceivedAutoConnect _receivedAutoConnect;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private UdpClient _dcsUdpListener;

        private volatile bool _stop;


        public DCSAutoConnectHandler(MainWindow.ReceivedAutoConnect receivedAutoConnect)
        {
            _receivedAutoConnect = receivedAutoConnect;

            StartDcsBroadcastListener();

            var args = Environment.GetCommandLineArgs();

            foreach (var arg in args)
            {
                if (arg.StartsWith("-host="))
                {
                    string host = arg.Replace("-host=", "").Trim();
                    HandleMessage(host);
                    Logger.Info("Auto Connect Launch for host: " + host);
                }
            }
        }

        private void StartDcsBroadcastListener()
        {
            _dcsUdpListener = new UdpClient();
            _dcsUdpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _dcsUdpListener.ExclusiveAddressUse = false;

            var localEp = new IPEndPoint(IPAddress.Any, 5069);
            _dcsUdpListener.Client.Bind(localEp);


            Task.Factory.StartNew(() =>
            {
                using (_dcsUdpListener)
                {
                    while (!_stop)
                    {
                        try
                        {
                            var groupEp = new IPEndPoint(IPAddress.Any, 5069);
                            var bytes = _dcsUdpListener.Receive(ref groupEp);

                            var message = Encoding.UTF8.GetString(
                                bytes, 0, bytes.Length);

                            HandleMessage(message);
                        }
                        catch (SocketException e)
                        {
                            // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                            if (!_stop)
                            {
                                Logger.Error(e, "SocketException Handling DCS AutoConnect Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "Exception Handling DCS AutoConnect Message");
                        }
                    }

                    try
                    {
                        _dcsUdpListener.Close();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception stoping DCS AutoConnect listener ");
                    }
                }
            });
        }

        private void HandleMessage(string message)
        {
            var address = message.Split(':');
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                new ThreadStart(delegate
                {
                    message = message.Trim();
                    if (message.Contains(':'))
                    {
                        try
                        {
                            _receivedAutoConnect(address[0].Trim(), int.Parse(address[1].Trim()));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Exception Parsing DCS AutoConnect Message");
                        }
                    }
                    else
                    {
                        _receivedAutoConnect(message, 5002);
                    }
                }));
        }


        public void Stop()
        {
            _stop = true;

            try
            {
                _dcsUdpListener.Close();
            }
            catch (Exception ex)
            {
                //IGNORE
            }
        }
    }
}
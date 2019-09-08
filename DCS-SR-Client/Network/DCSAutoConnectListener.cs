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

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network
{
    public class DCSAutoConnectListener
    {
        private readonly MainWindow.ReceivedAutoConnect _receivedAutoConnect;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private UdpClient _dcsUdpListener;

        private volatile bool _stop;


        public DCSAutoConnectListener(MainWindow.ReceivedAutoConnect receivedAutoConnect)
        {
        }


        private void StartDcsBroadcastListener()
        {
        }

        private void HandleMessage(string message)
        {
        }


        public void Stop()
        {
        }
    }
}
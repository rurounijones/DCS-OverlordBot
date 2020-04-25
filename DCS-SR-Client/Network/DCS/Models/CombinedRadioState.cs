using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models
{
    public struct CombinedRadioState
    {
        public DCSPlayerRadioInfo RadioInfo;

        public RadioSendingState RadioSendingState;

        public RadioReceivingState[] RadioReceivingState;

        public int ClientCountConnected;

        public int ClientCountIngame;

        public int[] TunedClients;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network
{

    /**
       * UDP PACKET LAYOUT
       *
       * - HEADER SEGMENT
       * UInt16 Packet Length - 2 bytes
       * double latitude - 8 bytes
       * double longitude - 8 bytes
       * double height - 8 bytes
       * Bytes / ASCII String GUID - 22 bytes
       */
    class UDPPositionPacket
    {
    }
}

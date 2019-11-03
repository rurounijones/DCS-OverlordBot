using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Network
{
    public struct ClientListExport
    {
        public ICollection<SRClient> Clients { get; set; }

        public string ServerVersion { get; set; }
    }
}

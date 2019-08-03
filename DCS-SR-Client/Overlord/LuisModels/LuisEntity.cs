using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels
{
    class LuisEntity
    {
        public string Entity { get; set; }
        public string Type { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public float Score { get; set; }
        public string Role { get; set; }
        public LuisResolution Resolution { get; set; }
    }

    class LuisResolution
    {
        public List<string> Values { get; set; }
        public string Subtype { get; set; }
        public string Value { get; set; }
    }
}

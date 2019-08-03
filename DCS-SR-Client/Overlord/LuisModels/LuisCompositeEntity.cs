using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels
{
    class LuisCompositeEntity
    {
        public string ParentType { get; set; }
        public string Value { get; set; }
        public List<Dictionary<string, string>> Children { get; set; }
    }
}

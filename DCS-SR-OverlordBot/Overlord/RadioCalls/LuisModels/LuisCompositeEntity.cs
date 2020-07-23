using System.Collections.Generic;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls.LuisModels
{
    public class LuisCompositeEntity
    {
        public string ParentType { get; set; }
        public string Value { get; set; }
        public List<Dictionary<string, string>> Children { get; set; }
    }
}

using System.Collections.Generic;

namespace RurouniJones.DCS.OverlordBot.RadioCalls.LuisModels
{
    public class LuisCompositeEntity
    {
        public string ParentType { get; set; }
        public string Value { get; set; }
        public List<Dictionary<string, string>> Children { get; set; }
    }
}

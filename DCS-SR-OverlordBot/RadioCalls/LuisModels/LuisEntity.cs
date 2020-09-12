using System.Collections.Generic;

namespace RurouniJones.DCS.OverlordBot.RadioCalls.LuisModels
{
    public class LuisEntity
    {
        public string Entity { get; set; }
        public string Type { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public float Score { get; set; }
        public string Role { get; set; }
        public LuisResolution Resolution { get; set; }
    }

    public class LuisResolution
    {
        public List<string> Values { get; set; }
        public string Subtype { get; set; }
        public string Value { get; set; }
    }
}

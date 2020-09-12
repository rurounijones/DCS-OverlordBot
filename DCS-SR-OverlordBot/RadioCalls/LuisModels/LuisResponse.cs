using System.Collections.Generic;

namespace RurouniJones.DCS.OverlordBot.RadioCalls.LuisModels
{
    public class LuisResponse
    {
        public string Query { get; set; }
        public Dictionary<string, string> TopScoringIntent { get; set; }
        public List<LuisEntity> Entities { get; set; }
        public List<LuisCompositeEntity> CompositeEntities { get; set; }
    }
}

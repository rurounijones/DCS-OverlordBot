using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.LuisModels
{
    class LuisResponse
    {
        public string Query { get; set; }
        public Dictionary<string, string> TopScoringIntent { get; set; }
        public List<LuisEntity> Entities { get; set; }
        public List<LuisCompositeEntity> CompositeEntities { get; set; }
    }
}

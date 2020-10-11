using System.Collections.Generic;
using System.Linq;
using NLog;
using QuikGraph;
using RurouniJones.DCS.Airfields.Structure;

namespace RurouniJones.DCS.Airfields.Controllers
{
    public abstract class BaseController
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected readonly Airfield _airfield;

        protected BaseController(Airfield airfield)
        {
            _airfield = airfield;
        }

        protected double PathCost(IEnumerable<TaggedEdge<NavigationPoint, string>> path)
        {
            return path.Sum(edge => _airfield.NavigationCost[edge]);
        }
    }
}

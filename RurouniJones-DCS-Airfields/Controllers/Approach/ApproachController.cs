using System.Collections.Generic;
using System.Linq;
using Geo.Geometries;
using QuikGraph;
using QuikGraph.Algorithms;
using RurouniJones.DCS.Airfields.Controllers.Util;
using RurouniJones.DCS.Airfields.Structure;

namespace RurouniJones.DCS.Airfields.Controllers.Approach
{
    public class ApproachController : BaseController
    {
        public ApproachController(Airfield airfield) : base(airfield) {}

        public List<NavigationPoint> GetApproachRoute(Point callerPosition)
        {
            var paths = GetPathsToActiveRunways();
            var taggedEdges = paths.OrderBy(x => x[0].Source.DistanceTo(callerPosition.Coordinate)).First();
            var navigationPoints = taggedEdges.Select(edge => edge.Source).ToList();
            // Add the runway itself
            navigationPoints.Add(taggedEdges.Last().Target);
            return navigationPoints;
        }

        private List<List<TaggedEdge<NavigationPoint, string>>> GetPathsToActiveRunways()
        {
            // First we get a list of active runway(s)
            var activeRunways = ActiveRunwayDecider.GetActiveRunways(_airfield);

            // Then we get a list of all the entry Waypoints
            var entryPoints = _airfield.WayPoints.Where(x => x.Name.ToLower().Contains("entry"));

            var paths = new List<KeyValuePair<double, List<TaggedEdge<NavigationPoint,string>>>>();

            // Get all the paths from all the entry waypoints to the active runway(s)
            foreach (var entryPoint in entryPoints)
            {
                var tryGetPaths =
                    _airfield.NavigationGraph.ShortestPathsDijkstra(_airfield.NavigationCostFunction, entryPoint);

                foreach (var runway in activeRunways)
                {
                    if (!tryGetPaths(runway, out var path)) continue;
                    var taggedEdges = path.ToList();
                    var pathCost = PathCost(taggedEdges);
                    var pathWithCost = new KeyValuePair<double, List<TaggedEdge<NavigationPoint,string>>>(pathCost, taggedEdges);
                    paths.Add(pathWithCost);
                }
            }

            // Sort so that we get the cheapest ones first
            var cheapestPathByCost = paths.OrderBy(pair => pair.Key).ToList();

            // This is the cheapest one, so we want this one and any that have the same cost
            // since there could be multiple entry points for a given active runway(s).
            var cheapestCost = paths[0].Key;

            cheapestPathByCost.RemoveAll(pair => pair.Key > cheapestCost);

            // And finally return the paths so the caller can find the one with the closest
            // start point to the player, which will be the actual approach path.
            return cheapestPathByCost.Select(pathPair => pathPair.Value).ToList();
        }
    }
}
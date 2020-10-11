using System;
using System.Collections.Generic;
using System.Linq;
using Geo.Geometries;
using QuikGraph;
using QuikGraph.Algorithms;
using RurouniJones.DCS.Airfields.Controllers.Util;
using RurouniJones.DCS.Airfields.Structure;

namespace RurouniJones.DCS.Airfields.Controllers.Ground
{
    public class GroundController : BaseController
    {
        public GroundController(Airfield airfield) : base(airfield) {}

        public TaxiInstructions GetTaxiInstructions(Point callerPosition)
        {
            TaxiPoint source;
            try
            {
                source = _airfield.TaxiPoints.OrderBy(taxiPoint => taxiPoint.DistanceTo(callerPosition.Coordinate))
                    .First();
            }
            catch (NullReferenceException)
            {
                throw new TaxiPathNotFoundException($"Taxi path not available because no TaxiPoints found");
            }

            Logger.Debug($"Player is at {callerPosition.Coordinate}, nearest Taxi point is {source}");
            var runways = ActiveRunwayDecider.GetActiveRunways(_airfield);
            return runways.Count == 1 ? GetTaxiInstructionsWhenSingleRunway(source, runways.First()) : GetTaxiInstructionsWhenMultipleRunways(source, runways);
        }

        private TaxiInstructions GetTaxiInstructionsWhenMultipleRunways(TaxiPoint source, List<Runway> runways)
        {
            var tryGetPaths = _airfield.NavigationGraph.ShortestPathsDijkstra(_airfield.NavigationCostFunction, source);

            var cheapestPathCost = double.PositiveInfinity;
            IEnumerable<TaggedEdge<NavigationPoint, string>> cheapestPath = new List<TaggedEdge<NavigationPoint, string>>();
            Runway closestRunway = null;

            foreach(var runway in runways)
            {
                if (!tryGetPaths(runway, out var path)) continue;
                var taggedEdges = path.ToList();
                var pathCost = PathCost(taggedEdges);
                if (!(pathCost < cheapestPathCost)) continue;
                closestRunway = runway;
                cheapestPath = taggedEdges;
                cheapestPathCost = pathCost;
            }
            return CompileInstructions(closestRunway, cheapestPath);
        }

        private TaxiInstructions GetTaxiInstructionsWhenSingleRunway(TaxiPoint source, Runway target)
        {
            var tryGetPaths = _airfield.NavigationGraph.ShortestPathsDijkstra(_airfield.NavigationCostFunction, source);
            if (tryGetPaths(target, out var path))
            {
                return CompileInstructions(target, path);
            }
            throw new TaxiPathNotFoundException($"No taxi path found from {source.Name} to {target.Name}");
        }

        private static TaxiInstructions CompileInstructions(NavigationPoint target, IEnumerable<TaggedEdge<NavigationPoint, string>> path)
        {
            var taggedEdges = path.ToList();

            var taxiInstructions = new TaxiInstructions()
            {
                DestinationName = target.Name,
                TaxiPoints = taggedEdges.Select(edge => edge.Source).ToList(),
                Comments = new List<string>()
            };

            // Include the final NavigationPoint
            taxiInstructions.TaxiPoints.Add(taggedEdges.Last().Target);
            foreach (var edge in taggedEdges)
            {
                taxiInstructions.TaxiwayNames.Add(edge.Tag);
            }

            taxiInstructions.TaxiwayNames = RemoveRepeating(taxiInstructions.TaxiwayNames);

            Logger.Debug(taxiInstructions);

            return taxiInstructions;
        }

        private static List<string> RemoveRepeating(IReadOnlyList<string> taxiways)
        {
            var dedupedTaxiways = new List<string>();

            for (var i = 0; i < taxiways.Count; i++)
            {
                if (i == 0)
                {
                    dedupedTaxiways.Add(taxiways[0]);
                }
                else if (taxiways[i] != taxiways[i - 1])
                {
                    dedupedTaxiways.Add(taxiways[i]);
                }
            }
            return dedupedTaxiways;
        }
    }
}

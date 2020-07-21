using Geo.Geometries;
using NLog;
using QuikGraph;
using QuikGraph.Algorithms;
using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;
using System.Linq;

namespace RurouniJones.DCS.Airfields.Controllers
{
    public class GroundController
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Airfield Airfield;

        public GroundController(Airfield airfield)
        {
            Airfield = airfield;
        }

        /// <summary>
        /// Returns a list of the active runways based on where the wind is coming from. At the moment, very simplistic.
        /// We will need to make this airport specific since some airfields have other considerations
        /// </summary>
        /// <param name="windSourceHeading"></param>
        /// <returns></returns>
        public List<Runway> GetActiveRunways()
        {
            var adjustedWindSource = Airfield.WindSource + 360;
            var activeRunways = new List<Runway>();

            foreach(Runway runway in Airfield.Runways)
            {
                var runwayHeading = runway.Heading + 360;

                if(runwayHeading <= adjustedWindSource + 90 && runwayHeading >= adjustedWindSource - 90)
                {
                    activeRunways.Add(runway);
                }
            }

            return activeRunways;
        }

        public TaxiInstructions GetTaxiInstructions(Point callerPosition)
        {
            var source = Airfield.TaxiPoints.OrderBy(taxiPoint => taxiPoint.DistanceTo(callerPosition.Coordinate)).First();
            Logger.Debug($"Player is at {callerPosition.Coordinate}, nearest Taxi point is {source}");

            var runways = GetActiveRunways();
            if(runways.Count == 1)
            {
                return GetTaxiInstructionsWhenSingleRunway(source, runways.First());
            } else
            {
                return GetTaxiInstructionsWhenMultipleRunways(source, runways);
            }
        }

        private TaxiInstructions GetTaxiInstructionsWhenMultipleRunways(TaxiPoint source, List<Runway> runways)
        {
            TryFunc<TaxiPoint, IEnumerable<TaggedEdge<TaxiPoint, string>>> tryGetPaths = Airfield.TaxiNavigationGraph.ShortestPathsDijkstra(Airfield.TaxiwayCostFunction, source);

            var cheapestPathCost = double.PositiveInfinity;
            IEnumerable<TaggedEdge<TaxiPoint, string>> cheapestPath = new List<TaggedEdge<TaxiPoint, string>>();
            Runway closestRunway = null;

            foreach(Runway runway in runways)
            {
                if (tryGetPaths(runway, out IEnumerable<TaggedEdge<TaxiPoint, string>> path))
                {
                    var pathCost = PathCost(path);
                    if (pathCost < cheapestPathCost)
                    {
                        closestRunway = runway;
                        cheapestPath = path;
                        cheapestPathCost = pathCost;
                    }
                }
            }
            return CompileInstructions(closestRunway, cheapestPath);
        }

        private TaxiInstructions GetTaxiInstructionsWhenSingleRunway(TaxiPoint source, TaxiPoint target)
        {
            TryFunc<TaxiPoint, IEnumerable<TaggedEdge<TaxiPoint, string>>> tryGetPaths = Airfield.TaxiNavigationGraph.ShortestPathsDijkstra(Airfield.TaxiwayCostFunction, source);
            if (tryGetPaths(target, out IEnumerable<TaggedEdge<TaxiPoint, string>> path))
            {
                return CompileInstructions(target, path);
            }
            else
            {
                throw new TaxiPathNotFoundException($"No taxi path found from {source.Name} to {target.Name}");
            }
        }

        private double PathCost(IEnumerable<TaggedEdge<TaxiPoint, string>> path)
        {
            double pathCost = 0;
            foreach(TaggedEdge<TaxiPoint, string> edge in path)
            {
                pathCost += Airfield.TaxiwayCost[edge];
            }
            return pathCost;
        }

        private TaxiInstructions CompileInstructions(TaxiPoint target, IEnumerable<TaggedEdge<TaxiPoint, string>> path)
        {
            TaxiInstructions taxiInstructions = new TaxiInstructions()
            {
                DestinationName = target.Name
            };

            foreach (TaggedEdge<TaxiPoint, string> edge in path)
            {
                taxiInstructions.TaxiwayNames.Add(edge.Tag);
                if (edge.Source is Runway runway && edge != path.First())
                {
                    taxiInstructions.Comments.Add($"Cross {runway.Name} at your discretion");
                }
            }

            taxiInstructions.TaxiwayNames = RemoveRepeating(taxiInstructions.TaxiwayNames);

            return taxiInstructions;
        }

        private List<string> RemoveRepeating(List<string> taxiways)
        {
            List<string> dedupedTaxiways = new List<string>();

            for (int i = 0; i < taxiways.Count; i++)
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

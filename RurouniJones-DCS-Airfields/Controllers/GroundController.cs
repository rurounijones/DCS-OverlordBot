using Geo.Geometries;
using NLog;
using QuikGraph;
using QuikGraph.Algorithms;
using RurouniJones.DCS.Airfields.Structure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RurouniJones.DCS.Airfields.Controllers
{
    public class GroundController
    {

        private static readonly Random randomizer = new Random();

        private Array instructionsVariants = new ArrayList() {"", "taxi to", "proceed to", "head to" }.ToArray();
        private Array viaVariants = new ArrayList() { "via", "along", "by way of", "using" }.ToArray();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Airfield Airfield;

        public GroundController(Airfield airfield)
        {
            Airfield = airfield;
        }

        public string GetTaxiInstructions(Point callerPosition, TaxiPoint target)
        {
            var source = Airfield.TaxiPoints.OrderBy(taxiPoint => taxiPoint.DistanceTo(callerPosition.Coordinate)).First();
            Logger.Debug($"Player is at {callerPosition.Coordinate}, nearest Taxi point is {source}");
            return GetTaxiInstructions(source, target);
        }

        public string GetTaxiInstructions(TaxiPoint source, TaxiPoint target)
        {
            TryFunc<TaxiPoint, IEnumerable<TaggedEdge<TaxiPoint, string>>> tryGetPaths = Airfield.TaxiNavigationGraph.ShortestPathsDijkstra(Airfield.TaxiwayCostFunction, source);
            List<string> comments = new List<string>();

            if (tryGetPaths(target, out IEnumerable<TaggedEdge<TaxiPoint, string>> path))
            {
                List<string> taxiways = new List<string>();
                foreach (TaggedEdge<TaxiPoint, string> edge in path)
                {
                    taxiways.Add(edge.Tag);
                    if (edge.Source is Runway runway && edge != path.First())
                    {
                        comments.Add($"Cross {runway.Name} at your discretion");
                    }
                }
                string instructions = $"{Random(instructionsVariants)} {target.Name} ";
                
                if(taxiways.Count > 0)
                {
                    instructions += $" {Random(viaVariants)} {string.Join("<break time=\"60ms\" /> ", RemoveRepeating(taxiways))}";
                }

                if (comments.Count > 0)
                {
                    instructions += $", {string.Join(", ", comments)}";
                }

                return instructions;
            }
            else
            {
                return $"Could not find a path from {source.Name} to {target.Name}";
            }
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

        private string Random(Array array)
        {
            return array.GetValue(randomizer.Next(array.Length)).ToString();
        }

    }
}

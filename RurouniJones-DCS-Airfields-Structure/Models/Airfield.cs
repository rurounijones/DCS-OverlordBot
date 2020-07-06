using Geo.Geometries;
using Newtonsoft.Json;
using NLog;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Graphviz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace RurouniJones.DCS.Airfields.Structure
{
    public class Airfield
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Name of the Airfield.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Decimal latitude (e.g. 41.12324)
        /// </summary>
        [JsonProperty(PropertyName = "lat")]
        public double Latitude { get; set; }

        /// <summary>
        /// Decimal Longitude (e.g. 37.12324)
        /// </summary>
        [JsonProperty(PropertyName = "lon")]
        public double Longitude { get; set; }

        /// <summary>
        /// Altitude in Meters
        /// </summary>
        [JsonProperty(PropertyName = "alt")]
        public double Altitude { get; set; }

        [JsonProperty(PropertyName = "dotGraph")]
        /// <summary>
        /// Name of the Airfield.
        /// </summary>
        public bool DotGraph { get; set; }

        /// <summary>
        /// A list of Parking spots
        /// 
        /// A Parking Spot includes anywhere where aircraft might spawn. So this needs to cover areas where a mission
        /// maker might spawn "OnGround" aircraft such as Harriers. For this reason the Parking Spots at Anapa include
        /// the Maintenance Area as Harriers are spawned there on GAW.
        /// </summary>
        [JsonProperty(PropertyName = "parkingSpots")]
        public List<ParkingSpot> ParkingSpots { get; set; }

        /// <summary>
        /// A list of Runways 
        /// </summary>
        [JsonProperty(PropertyName = "runways")]
        public List<Runway> Runways { get; set; }

        /// <summary>
        /// A list of Taxi Junctions
        /// 
        /// A Taxi Junction is any place where two taxiways meet each other and where they meet either a Parking Spot
        /// or a Runway
        /// </summary>
        [JsonProperty(PropertyName = "junctions")]
        public List<Junction> Junctions { get; set; }

        /// <summary>
        /// A list of Taxipaths
        /// 
        /// A taxi path is a taxiway with a specific source and target TaxiPoint.
        /// If a taxiway is to be navigated in both directions then it needs two taxipaths, one going each way.
        /// </summary>
        [JsonProperty(PropertyName = "taxipaths")]
        public List<TaxiPath> Taxiways { get; set; }

        /// <summary>
        /// Blah
        /// </summary>
        public Point Position {
            get {
                return new Point(Latitude, Longitude, Altitude);
            }
        }

        private readonly AdjacencyGraph<TaxiPoint, TaggedEdge<TaxiPoint, string>> TaxiNavigationGraph = new AdjacencyGraph<TaxiPoint, TaggedEdge<TaxiPoint, string>>();
        private Dictionary<TaggedEdge<TaxiPoint, string>, double> TaxiwayCost;
        private Func<TaggedEdge<TaxiPoint, string>, double> TaxiwayCostFunction;

        public IEnumerable<TaxiPoint> TaxiPoints {
            get {
                return TaxiNavigationGraph.Vertices;
            }
        }

        [OnDeserialized]
        public void BuildTaxiGraph(StreamingContext context)
        {
            foreach(Runway runway in Runways)
            {
                TaxiNavigationGraph.AddVertex(runway);
            }
            foreach (ParkingSpot parkingSpot in ParkingSpots)
            {
                TaxiNavigationGraph.AddVertex(parkingSpot);
            }
            foreach (Junction junction in Junctions)
            {
                TaxiNavigationGraph.AddVertex(junction);
            }

            TaxiwayCost = new Dictionary<TaggedEdge<TaxiPoint, string>, double>(TaxiNavigationGraph.EdgeCount);

            foreach (TaxiPath taxiway in Taxiways)
            {
                TaxiPoint source = TaxiNavigationGraph.Vertices.First(taxiPoint => taxiPoint.Name.Equals(taxiway.Source));
                TaxiPoint target = TaxiNavigationGraph.Vertices.First(taxiPoint => taxiPoint.Name.Equals(taxiway.Target));
                string tag = taxiway.Name;

                TaggedEdge<TaxiPoint, string> edge = new TaggedEdge<TaxiPoint, string>(source, target, tag);

                TaxiNavigationGraph.AddEdge(edge);

                TaxiwayCost.Add(edge, taxiway.Cost);
            }

            TaxiwayCostFunction = AlgorithmExtensions.GetIndexer(TaxiwayCost);

            if (DotGraph == true)
            {
                OutputDotGraph();
            }
        }

        /**
         * TODO: This should be moved to some sort of ATC Ground Class.
         */
        public string GetTaxiInstructions(TaxiPoint source, TaxiPoint target)
        {
            TryFunc<TaxiPoint, IEnumerable<TaggedEdge<TaxiPoint, string>>> tryGetPaths = TaxiNavigationGraph.ShortestPathsDijkstra(TaxiwayCostFunction, source);
            List<string> comments = new List<string>();

            if (tryGetPaths(target, out IEnumerable<TaggedEdge<TaxiPoint, string>> path))
            {
                List<string> taxiways = new List<string>();
                foreach (TaggedEdge<TaxiPoint, string> edge in path)
                {
                    taxiways.Add(edge.Tag);
                    if (edge.Source is Runway runway)
                    {
                        comments.Add($"Cross {runway.Name}");
                    }
                }
                string instructions = $"Taxi to {target.Name} via {string.Join(" ", RemoveRepeating(taxiways))}";
                if(comments.Count > 0)
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

        /**
         * TODO: Get rid of this when we have a nice little separate tool that does all the creation of the graphs.
         */
        private void OutputDotGraph()
        {
            string dotGraph = TaxiNavigationGraph.ToGraphviz(algorithm =>
            {
                // Custom init example
                algorithm.FormatVertex += (sender, vertexArgs) =>
                {
                    vertexArgs.VertexFormat.Label = $"{vertexArgs.Vertex.Name}";
                };
                algorithm.FormatEdge += (sender, edgeArgs) =>
                {
                    var label = new QuikGraph.Graphviz.Dot.GraphvizEdgeLabel
                    {
                        Value = $"{edgeArgs.Edge.Tag} : {TaxiwayCost[edgeArgs.Edge]}"
                    };
                    edgeArgs.EdgeFormat.Label = label;
                };
            });

            Logger.Debug(Name + " \n" + dotGraph);
        }
    }
}

using Geo.Geometries;
using Newtonsoft.Json;
using NLog;
using QuikGraph;
using QuikGraph.Graphviz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation
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
        /// Blah
        /// </summary>
        [JsonProperty(PropertyName = "parkingSpots")]
        public List<ParkingSpot> ParkingSpots { get; set; }

        /// <summary>
        /// Blah
        /// </summary>
        [JsonProperty(PropertyName = "runways")]
        public List<Runway> Runways { get; set; }

        /// <summary>
        /// Blah
        /// </summary>
        [JsonProperty(PropertyName = "junctions")]
        public List<Junction> Junctions { get; set; }

        /// <summary>
        /// Blah
        /// </summary>
        [JsonProperty(PropertyName = "taxiways")]
        public List<Taxiway> Taxiways { get; set; }

        public Point Position {
            get {
                return new Point(Latitude, Longitude, Altitude);
            }
        }

        private UndirectedGraph<TaxiPoint, TaggedEdge<TaxiPoint, string>> TaxiNavigationGraph = new UndirectedGraph<TaxiPoint, TaggedEdge<TaxiPoint, string>>();
        private Dictionary<TaggedEdge<TaxiPoint, string>, double> TaxiwayCost;

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

            foreach (Taxiway taxiway in Taxiways)
            {
                TaxiPoint source = TaxiNavigationGraph.Vertices.First(taxiPoint => taxiPoint.Name.Equals(taxiway.Connections[0]));
                TaxiPoint target = TaxiNavigationGraph.Vertices.First(taxiPoint => taxiPoint.Name.Equals(taxiway.Connections[1]));
                string tag = taxiway.Name;

                TaggedEdge<TaxiPoint, string> edge = new TaggedEdge<TaxiPoint, string>(source, target, tag);

                TaxiNavigationGraph.AddEdge(edge);

                TaxiwayCost.Add(edge, taxiway.Cost);
            }

            if(DotGraph == true)
            {
                OutputDotGraph();
            }
        }

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
                    var label = new QuikGraph.Graphviz.Dot.GraphvizEdgeLabel();
                    label.Value = $"{edgeArgs.Edge.Tag} : {TaxiwayCost[edgeArgs.Edge]}";
                    edgeArgs.EdgeFormat.Label = label;
                };
            });

            Logger.Debug(Name + "\n" + dotGraph);
        }

    }

}

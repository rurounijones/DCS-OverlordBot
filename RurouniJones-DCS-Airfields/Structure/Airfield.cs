using Geo.Geometries;
using Newtonsoft.Json;
using NLog;
using QuikGraph;
using QuikGraph.Algorithms;
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
        [JsonProperty(PropertyName = "name")]
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
        /// Position of the airfield 
        /// </summary>
        [JsonIgnore]
        public Point Position {
            get {
                return new Point(Latitude, Longitude, Altitude);
            }
        }

        /// <summary>
        /// Direciton the wind is COMING from
        /// </summary>
        [JsonIgnore]
        public int WindSource { get; set; } = 90;

        [JsonIgnore]
        public readonly AdjacencyGraph<TaxiPoint, TaggedEdge<TaxiPoint, string>> TaxiNavigationGraph = new AdjacencyGraph<TaxiPoint, TaggedEdge<TaxiPoint, string>>();

        [JsonIgnore]
        public Dictionary<TaggedEdge<TaxiPoint, string>, double> TaxiwayCost;

        [JsonIgnore]
        public Func<TaggedEdge<TaxiPoint, string>, double> TaxiwayCostFunction
        {
            get
            {
                return AlgorithmExtensions.GetIndexer(TaxiwayCost);
            }
        }

        [JsonIgnore]
        public IEnumerable<TaxiPoint> TaxiPoints {
            get {
                return TaxiNavigationGraph.Vertices;
            }
        }

        [OnDeserialized]
        public void BuildTaxiGraph(StreamingContext context)
        {
            Logger.Debug($"{this.Name} airfield JSON deserialized");
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
            Logger.Debug($"{this.Name} airfield navigation graph built");

        }
    }
}

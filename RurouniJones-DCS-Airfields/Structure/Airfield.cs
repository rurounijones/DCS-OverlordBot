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
        public List<ParkingSpot> ParkingSpots { get; set; } = new List<ParkingSpot>();

        /// <summary>
        /// A list of Runways 
        /// </summary>
        [JsonProperty(PropertyName = "runways")]
        public List<Runway> Runways { get; set; } = new List<Runway>();

        /// <summary>
        /// A list of all the nodes that make up a runway
        /// </summary>
        [JsonIgnore]
        public Dictionary<Runway, List<NavigationPoint>> RunwayNodes { get; set; } = new Dictionary<Runway, List<NavigationPoint>>();

        /// <summary>
        /// A list of Taxi Junctions
        /// 
        /// A Taxi Junction is any place where two taxiways meet each other and where they meet either a Parking Spot
        /// or a Runway
        /// </summary>
        [JsonProperty(PropertyName = "junctions")]
        public List<Junction> Junctions { get; set; } = new List<Junction>();

        /// <summary>
        /// A list of Taxipaths
        /// 
        /// A taxi path is a taxiway with a specific source and target NavigationPoint.
        /// If a taxiway is to be navigated in both directions then it needs two taxipaths, one going each way.
        /// </summary>
        [JsonProperty(PropertyName = "taxipaths")]
        public List<NavigationPath> Taxiways { get; set; } = new List<NavigationPath>();

        /// <summary>
        /// A list of Waypoints for inbound and outbound aircraft that are in the air.
        /// </summary>
        [JsonProperty(PropertyName = "waypoints")]
        public List<WayPoint> WayPoints { get; set; } = new List<WayPoint>();

        /// <summary>
        /// Position of the airfield 
        /// </summary>
        [JsonIgnore]
        public Point Position {
            get => _position != null ? _position : new Point(Latitude, Longitude, Altitude);
            set => _position = value;
        }
        private Point _position;

        /// <summary>
        /// Direction the wind is COMING from
        /// </summary>
        [JsonIgnore]
        public int WindHeading { get; set; } = 90;

        /// <summary>
        /// Wind speed 33ft (the standard measurement point) above the airfield in meters / second.
        /// </summary>
        [JsonIgnore]
        public double WindSpeed { get; set; } = 0;

        /// <summary>
        /// The coalition the airbase belongs to.
        /// </summary>
        [JsonIgnore]
        public int Coalition { get; set; } = 0;

        [JsonIgnore] public AdjacencyGraph<NavigationPoint, TaggedEdge<NavigationPoint, string>> NavigationGraph =
            new AdjacencyGraph<NavigationPoint, TaggedEdge<NavigationPoint, string>>();

        [JsonIgnore]
        public Dictionary<TaggedEdge<NavigationPoint, string>, double> NavigationCost;

        [JsonIgnore] public Func<TaggedEdge<NavigationPoint, string>, double> NavigationCostFunction;

        [JsonIgnore]
        public IEnumerable<TaxiPoint> TaxiPoints
        {
            get => NavigationGraph.Vertices.OfType<TaxiPoint>();
            set => _taxiPoints = value;
        }
        private IEnumerable<TaxiPoint> _taxiPoints;

        [OnDeserialized]
        public void BuildTaxiGraph(StreamingContext context)
        {
            try
            {
                Logger.Debug($"{Name} airfield JSON deserialized");

                Runways.ForEach(runway => NavigationGraph.AddVertex(runway));
                ParkingSpots.ForEach(parkingSpot => NavigationGraph.AddVertex(parkingSpot));
                Junctions.ForEach(junction => NavigationGraph.AddVertex(junction));
                WayPoints.ForEach(wayPoint => NavigationGraph.AddVertex(wayPoint));

                NavigationCost = new Dictionary<TaggedEdge<NavigationPoint, string>, double>(NavigationGraph.EdgeCount);

                foreach (NavigationPath taxiway in Taxiways)
                {
                    NavigationPoint source =
                        NavigationGraph.Vertices.First(taxiPoint => taxiPoint.Name.Equals(taxiway.Source));
                    NavigationPoint target =
                        NavigationGraph.Vertices.First(taxiPoint => taxiPoint.Name.Equals(taxiway.Target));
                    string tag = taxiway.Name;

                    TaggedEdge<NavigationPoint, string> edge =
                        new TaggedEdge<NavigationPoint, string>(source, target, tag);

                    NavigationGraph.AddEdge(edge);

                    NavigationCost.Add(edge, taxiway.Cost);
                }

                NavigationCostFunction = AlgorithmExtensions.GetIndexer(NavigationCost);
                Logger.Debug($"{Name} airfield navigation graph built");
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Could not build navigation graph for {Name}");
            }

            // Now populate the runway nodes so we know all the nodes that make up a runway
            try
            {
                foreach(var runway in Runways)
                {
                    NavigationPoint node = runway;
                    var nodes = new List<NavigationPoint> {node};
                    while (true)
                    {
                        var edges = NavigationGraph.Edges.Where(x => x.Source == node);
                        node = edges.First(x => x.Tag == "Runway" && !nodes.Contains(x.Target)).Target;
                        nodes.Add(node);
                        if (node is Runway) break;
                    }
                    RunwayNodes.Add(runway, nodes);
                    Logger.Debug($"{Name} {runway} nodes built");
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Could not build runway nodes for  graph for {Name}");
            }
        }
    }
}

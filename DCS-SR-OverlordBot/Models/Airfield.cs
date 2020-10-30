using System.Collections.Concurrent;
using RurouniJones.DCS.OverlordBot.Controllers;

namespace RurouniJones.DCS.OverlordBot.Models
{
    /// <summary>
    /// An active in-game airfield. Inherits structural information from the Airfields library
    /// but includes runtime information such as aircraft activity.
    /// </summary>
    public class Airfield : Airfields.Structure.Airfield
    {
        public ConcurrentDictionary<string, TaxiProgressChecker> TaxiingAircraft { get; } = new ConcurrentDictionary<string, TaxiProgressChecker>();
        public ConcurrentDictionary<string, ApproachChecker> ApproachingAircraft { get; } = new ConcurrentDictionary<string, ApproachChecker>();

        public Airfield() {}

        public Airfield(Airfields.Structure.Airfield airfieldStructure)
        {
            Name = airfieldStructure.Name;
            Latitude = airfieldStructure.Latitude;
            Longitude = airfieldStructure.Longitude;
            Altitude = airfieldStructure.Altitude;
            Position = airfieldStructure.Position;

            ParkingSpots = airfieldStructure.ParkingSpots;
            Runways = airfieldStructure.Runways;
            Junctions = airfieldStructure.Junctions;
            Taxiways = airfieldStructure.Taxiways;
            WayPoints = airfieldStructure.WayPoints;
            TaxiPoints = airfieldStructure.TaxiPoints;

            WindHeading = airfieldStructure.WindHeading;
            WindSpeed = airfieldStructure.WindSpeed;

            Coalition = airfieldStructure.Coalition;

            NavigationGraph = airfieldStructure.NavigationGraph;
            NavigationCost = airfieldStructure.NavigationCost;
            NavigationCostFunction = airfieldStructure.NavigationCostFunction;
        }
    }
}

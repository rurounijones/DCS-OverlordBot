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
    }
}

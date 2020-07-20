using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;


namespace RurouniJones.DCS.Airfields.Controllers.Tests
{
    public class GroundControllerAbstractTests
    {
        protected static Airfield Airfield;
        protected static GroundController Controller;

        protected string SayTaxiways(List<string> taxiways)
        {
            return string.Join(" <break time=\"60ms\" /> ", taxiways);
        }
    }
}

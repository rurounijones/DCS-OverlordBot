using Geo.Geometries;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RurouniJones.DCS.Airfields.Structure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RurouniJones.DCS.Airfields.Controllers.Tests
{
    [TestClass]
    public class AnapaTests
    {
        private static Airfield Airfield;
        private static GroundController Controller;

        [ClassInitialize]
        public static void Setup(TestContext _)
        {
            Airfield = Populator.Airfields.First(airfield => airfield.Name.Equals("Anapa-Vityazevo"));
            Controller = new GroundController(Airfield);
        }

        [TestMethod]
        public void TestApronOneToRunwayFour()
        {
            Airfield.WindSource = 90;
            Point startPoint = new Point(45.0101581, 37.3481765); // On Apron 1
            var expectedRunway = "Runway-0 4";
            var expectedTaxiWays = new List<string>() { "Mike", "Alpha" };

            string instructions = Controller.GetTaxiInstructions(startPoint);

            StringAssert.Contains(instructions, expectedRunway);
            StringAssert.Contains(instructions, SayTaxiways(expectedTaxiWays) );
        }

        private string SayTaxiways(List<string> taxiways)
        {
            return string.Join("<break time=\"60ms\" /> ", taxiways);
        }
    }
}

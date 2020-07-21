using Geo.Geometries;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;
using System.Linq;

namespace RurouniJones.DCS.Airfields.Controllers.Tests
{
    [TestClass]
    public class KrasnodarPashkovskyTests : GroundControllerAbstractTests
    {
        [ClassInitialize]
        public static void Setup(TestContext _)
        {
            Airfield = Populator.Airfields.First(airfield => airfield.Name.Equals("Krasnodar-Pashkovsky"));
            Controller = new GroundController(Airfield);
        }

        [TestMethod]
        public void TestApronAToRunwayFiveLeft()
        {
            Airfield.WindSource = 90;
            Point startPoint = new Point(45.038881971882, 39.14140614585);
            TaxiInstructions expected = new TaxiInstructions()
            {
                DestinationName = "Runway-0 5 Left",
                TaxiwayNames = new List<string>() { "Alfa" }
            };

            AssertInstructions(expected, Controller.GetTaxiInstructions(startPoint));
        }

        [TestMethod]
        public void TestApronOneToRunwayFiveRight()
        {
            Airfield.WindSource = 90;
            Point startPoint = new Point(45.047601257612, 39.200777416505);
            TaxiInstructions expected = new TaxiInstructions()
            {
                DestinationName = "Runway-0 5 Right",
                TaxiwayNames = new List<string>() { "November", "Echo" }
            };

            AssertInstructions(expected, Controller.GetTaxiInstructions(startPoint));
        }
    }
}

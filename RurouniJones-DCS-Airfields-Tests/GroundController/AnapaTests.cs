using Geo.Geometries;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;
using System.Linq;

namespace RurouniJones.DCS.Airfields.Controllers.Tests
{
    [TestClass]
    public class AnapaTests : GroundControllerAbstractTests
    {
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
            Point startPoint = new Point(45.0101581, 37.3481765);
            TaxiInstructions expected = new TaxiInstructions()
            {
                DestinationName = "Runway-0 4",
                TaxiwayNames = new List<string>() { "Mike", "Alpha" }
            };

            AssertInstructions(expected, Controller.GetTaxiInstructions(startPoint));
        }
    }
}

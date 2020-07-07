using Geo.Geometries;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RurouniJones.DCS.Airfields.Structure;
using System.Linq;

namespace RurouniJones.DCS.Airfields.Controllers.Tests
{
    [TestClass]
    public class AnapaTests
    {
        private static Airfield Airfield;
        private static GroundController Controller;

        [ClassInitialize]
        public static void SetAirfield(TestContext _)
        {
            Airfield = Populator.Airfields.First(airfield => airfield.Name.Equals("Anapa-Vityazevo"));
            Controller = new GroundController(Airfield);
        }


        [TestMethod]
        public void TestApronOnenToRunwayFour()
        {
            Point position = new Point(45.0101581, 37.3481765); // On Apron 1
            Runway target = (Runway)GetTaxiPoint("Runway 0 4");

            string instructions = Controller.GetTaxiInstructions(position, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Mike Alpha", instructions);
        }

        private TaxiPoint GetTaxiPoint(string name)
        {
            return Airfield.TaxiPoints.First(taxiPoint => taxiPoint.Name.Equals(name));
        }
    }
}

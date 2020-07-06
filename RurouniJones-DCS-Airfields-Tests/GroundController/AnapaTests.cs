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
        public void TestApronOneToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Apron 1");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Controller.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestApronTwoToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Apron 2");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Controller.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestWhiskeySpotsToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Whiskey Spots");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Controller.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Whiskey Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestMaintenanceAreaToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Maintenance Area");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Controller.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via November Whiskey Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestEastApronToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("East Apron");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Controller.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Echo Delta Mike Alpha, Cross Runway 2 2", instructions);
        }

        [TestMethod]
        public void TestEchoSpotsToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Echo Spots");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Controller.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Echo Delta Mike Alpha, Cross Runway 2 2", instructions);
        }

        private TaxiPoint GetTaxiPoint(string name)
        {
            return Airfield.TaxiPoints.First(taxiPoint => taxiPoint.Name.Equals(name));
        }
    }
}

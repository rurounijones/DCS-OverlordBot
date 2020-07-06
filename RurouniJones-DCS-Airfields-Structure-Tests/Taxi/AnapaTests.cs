using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace RurouniJones.DCS.Airfields.Structure.Tests
{
    [TestClass]
    public class AnapaTests
    {
        private static readonly Airfield Anapa = Populator.Airfields.First(airfield => airfield.Name.Equals("Anapa-Vityazevo"));

        [TestMethod]
        public void TestApronOneToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Apron 1");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Anapa.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestApronTwoToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Apron 2");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Anapa.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestWhiskeySpotsToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Whiskey Spots");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Anapa.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Whiskey Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestMaintenanceAreaToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Maintenance Area");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Anapa.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via November Whiskey Mike Alpha", instructions);
        }

        [TestMethod]
        public void TestEastApronToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("East Apron");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Anapa.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Echo Delta Mike Alpha, Cross Runway 2 2", instructions);
        }

        [TestMethod]
        public void TestEchoSpotsToRunwayFour()
        {
            ParkingSpot source = (ParkingSpot) GetTaxiPoint("Echo Spots");
            Runway target = (Runway) GetTaxiPoint("Runway 0 4");

            string instructions = Anapa.GetTaxiInstructions(source, target);

            Assert.AreEqual("Taxi to Runway 0 4 via Echo Delta Mike Alpha, Cross Runway 2 2", instructions);
        }

        private TaxiPoint GetTaxiPoint(string name)
        {
            return Anapa.TaxiPoints.First(taxiPoint => taxiPoint.Name.Equals(name));
        }
    }
}

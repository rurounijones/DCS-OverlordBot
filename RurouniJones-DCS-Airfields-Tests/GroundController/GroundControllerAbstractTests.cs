using Microsoft.VisualStudio.TestTools.UnitTesting;
using RurouniJones.DCS.Airfields.Structure;

namespace RurouniJones.DCS.Airfields.Controllers.Tests
{
    public class GroundControllerAbstractTests
    {
        protected static Airfield Airfield;
        protected static GroundController Controller;

        public static void AssertInstructions(TaxiInstructions expected, TaxiInstructions actual)
        {
            StringAssert.Contains(expected.DestinationName, actual.DestinationName);
            CollectionAssert.AreEqual(expected.TaxiwayNames, actual.TaxiwayNames);
            CollectionAssert.AreEqual(expected.Comments, actual.Comments);
        }
    }
}

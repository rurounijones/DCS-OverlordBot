using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RurouniJones.DCS.OverlordBot.Util.Tests
{
    [TestClass]
    class LevenshteinDistanceTests
    {
        [TestMethod]
        public void WhenStringsEqualReturnZero()
        {
            Assert.IsTrue(LevenshteinDistance.Calculate("RurouniJones", "RurouniJones") == 0);
        }

        [TestMethod]
        public void WhenOneStringEmptyReturnLengthOfTheOther()
        {
            Assert.IsTrue(LevenshteinDistance.Calculate("RurouniJones", "") == "RurouniJones".Length);
            Assert.IsTrue(LevenshteinDistance.Calculate("", "RurouniJones") == "RurouniJones".Length);
        }

        [TestMethod]
        public void WhenStringsSimilarReturnLevenshteinDistance()
        {
            Assert.IsTrue(LevenshteinDistance.Calculate("Kodiak", "Kodak") == 1);
            Assert.IsTrue(LevenshteinDistance.Calculate("Ram", "Raman") == 2);
            Assert.IsTrue(LevenshteinDistance.Calculate("Mama", "Mom") == 2);
            Assert.IsTrue(LevenshteinDistance.Calculate("imax", "volmax") == 3);
        }
    }
}
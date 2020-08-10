using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Util.Tests
{
    [TestClass]
    public class GeospatialTests
    {
        [TestMethod]
        public void WhenPointInCaucasusReturnTrue()
        {
            var point = new Geo.Geometries.Point(44.961383022734175, 37.985886938697085);
            Assert.IsTrue(Geospatial.IsCaucasus(point));

        }

        [TestMethod]
        public void WhenPointNotInCaucasusReturnFalse()
        {
            var point = new Geo.Geometries.Point(24.2577778, 54.5341667);
            Assert.IsFalse(Geospatial.IsCaucasus(point));
        }
    }
}
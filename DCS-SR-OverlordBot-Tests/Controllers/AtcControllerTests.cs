using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RurouniJones.DCS.OverlordBot.Controllers.Tests
{

    [TestClass]
    public class AtcControllerTests
    {
        [TestClass]
        public class NoneMethod
        {
            private AtcController controller;

            [TestInitialize]
            public void Init()
            {
                controller = new AtcController();
            }

            [TestMethod]
            public void IgnoresTheCall()
            {
                Assert.IsTrue(true);
            }
        }
    }
}

using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Controllers.Tests
{

    [TestClass]
    public class AwacsControllerTests
    {

        [TestClass]
        public class NoneMethod
        {
            private AwacsController Controller;

            [TestInitialize]
            public void Init()
            {
                Controller = new AwacsController();
            }

            [TestMethod]
            public void IgnoresTheCall()
            {
                var mock = new Mock<BaseRadioCall>("");
                var radioCall = mock.Object;

                string response = Controller.None(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class ReadyToTaxiMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<BaseRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<BaseRadioCall>("");
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderItIsAnAwacsFrequency()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Krymsk");
                _mock.SetupGet(call => call.AwacsCallsign).Returns((string)null);
                _mock.SetupGet(call => call.AirbaseName).Returns("Krymsk");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Overlord, this is an AWACS frequency.";
                string response = _controller.ReadyToTaxi(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_TellsTheSenderItIsAnAwacsFrequency()
            {
                _controller.Callsign = "Magic";

                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Krymsk");
                _mock.SetupGet(call => call.AwacsCallsign).Returns((string)null);
                _mock.SetupGet(call => call.AirbaseName).Returns("Krymsk");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Magic, this is an AWACS frequency.";
                string response = _controller.ReadyToTaxi(radioCall);

                Assert.AreEqual(expected, response);
            }
        }

        [TestClass]
        public class NullSenderMethod {

            private AwacsController _controller;
            private Mock<BaseRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();
                _mock = new Mock<BaseRadioCall>("");
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderItCouldNotRecogniseTheCallsign()
            {
                _mock.SetupGet(call => call.Sender).Returns((Player)null);

                var radioCall = _mock.Object;

                string expected = "Last transmitter, I could not recognise your call-sign.";
                string response = _controller.NullSender(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_AndSenderAdressesWrongAwacs_IgnoresTheTransmission()
            {
                _mock = new Mock<BaseRadioCall>("");
                
                _controller.Callsign = "Darkstar";

                _mock.SetupGet(call => call.Sender).Returns((Player)null);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string response = _controller.NullSender(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class UnVerifiedSenderMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<BaseRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<BaseRadioCall>("");
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderItCannotBeFoundOnScope()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Darkstar");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Darkstar");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Darkstar, I cannot find you on scope.";
                string response = _controller.UnverifiedSender(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_AndReceiverIsAnyface_TellsTheSenderItCannotBeFoundOnScope()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("anyface");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("anyface");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Overlord, I cannot find you on scope.";
                string response = _controller.UnverifiedSender(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_TellsTheSenderItCannotBeFoundOnScope()
            {
                _controller.Callsign = "Magic";

                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Magic, I cannot find you on scope.";
                string response = _controller.UnverifiedSender(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_AndSenderAdressesWrongAwacs_IgnoresTheTransmission()
            {
                _controller.Callsign = "Darkstar";

                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string response = _controller.UnverifiedSender(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class PictureMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<BaseRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<BaseRadioCall>("");
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderPictureCallsAreNotSupported()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Darkstar");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Darkstar");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Darkstar, we do not support picture calls.";
                string response = _controller.Picture(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_AndReceiverIsAnyface_TellsTheSenderPictureCallsAreNotSupported()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("anyface");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("anyface");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Overlord, we do not support picture calls.";
                string response = _controller.Picture(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_TellsTheSenderPictureCallsAreNotSupported()
            {
                _controller.Callsign = "Magic";

                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Magic, we do not support picture calls.";
                string response = _controller.Picture(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_AndSenderAdressesWrongAwacs_IgnoresTheTransmission()
            {
                _controller.Callsign = "Darkstar";

                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string response = _controller.Picture(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class UnknownMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<BaseRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<BaseRadioCall>("");
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderTransmissionCouldNotBeUnderstood()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Darkstar");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Darkstar");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Darkstar, I could not understand your transmission.";
                string response = _controller.Unknown(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_AndReceiverIsAnyface_TellsTheSenderTransmissionCouldNotBeUnderstood()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("anyface");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("anyface");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Overlord, I could not understand your transmission.";
                string response = _controller.Unknown(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_TellsTheSenderTransmissionCouldNotBeUnderstood()
            {
                _controller.Callsign = "Magic";

                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Magic, I could not understand your transmission.";
                string response = _controller.Unknown(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_AndSenderAdressesWrongAwacs_IgnoresTheTransmission()
            {
                _controller.Callsign = "Darkstar";

                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string response = _controller.Unknown(radioCall);

                Assert.IsNull(response);
            }
        }
    }
}

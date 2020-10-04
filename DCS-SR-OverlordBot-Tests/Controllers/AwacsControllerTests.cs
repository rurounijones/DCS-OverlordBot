using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RurouniJones.DCS.OverlordBot.GameState;
using RurouniJones.DCS.OverlordBot.RadioCalls;

namespace RurouniJones.DCS.OverlordBot.Controllers.Tests
{

    [TestClass]
    public class AwacsControllerTests
    {
        [TestClass]
        public class NoReceivedAwacsCallsign {

            private AwacsController _controller;
            private Player _sender;
            private Mock<IRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Id = "a1",
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<IRadioCall>();
                _mock.SetupGet(call => call.Intent).Returns("BogeyDope");

            }

            [TestMethod]
            public void WhenNoReceivedAwacsCallsign_ReturnsNull()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns((string) null);
                _mock.SetupGet(call => call.AwacsCallsign).Returns((string) null);

                var radioCall = _mock.Object;

                var response = _controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }

            [TestMethod]
            public void WhenNoReceivedAwacsCallsignButAirbaseReceived_ReturnsNull()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("krymsk");
                _mock.SetupGet(call => call.AirbaseName).Returns("krymsk");
                _mock.SetupGet(call => call.AwacsCallsign).Returns((string) null);

                var radioCall = _mock.Object;

                var response = _controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }
        }

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
                var mock = new Mock<IRadioCall>();
                mock.SetupGet(call => call.Intent).Returns("None");

                var radioCall = mock.Object;

                string response = Controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class BearingToAirbaseMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<IRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Id = "a1",
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<IRadioCall>();

            }

            [TestMethod]
            public void WhenNoReceivedAwacsCallsign_ReturnsNull()
            {
                _mock.SetupGet(call => call.Intent).Returns("BearingToAirbase");
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns((string) null);
                _mock.SetupGet(call => call.AwacsCallsign).Returns((string) null);
                _mock.SetupGet(call => call.AirbaseName).Returns("Krymsk");

                var radioCall = _mock.Object;

                var response = _controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class NullSenderMethod {

            private AwacsController _controller;
            private Mock<IRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();
                _mock = new Mock<IRadioCall>();
                _mock.SetupGet(call => call.Intent).Returns("BogeyDope");

            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderItCouldNotRecogniseTheCallsign()
            {
                _mock.SetupGet(call => call.Sender).Returns((Player)null);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string expected = "Last transmitter, I could not recognize your call-sign.";
                string response = _controller.ProcessRadioCall(radioCall);

                Assert.AreEqual(expected, response);
            }

            [TestMethod]
            public void WhenAssignedAwacsCallSign_AndSenderAddressesWrongAwacs_IgnoresTheTransmission()
            {
                _mock = new Mock<IRadioCall>();
                
                _controller.Callsign = "Darkstar";

                _mock.SetupGet(call => call.Sender).Returns((Player)null);
                _mock.SetupGet(call => call.ReceiverName).Returns("Magic");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Magic");

                var radioCall = _mock.Object;

                string response = _controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class UnVerifiedSenderMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<IRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Id = null,
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<IRadioCall>();
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderItCannotBeFoundOnScope()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Darkstar");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Darkstar");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Darkstar, I cannot find you on scope.";
                string response = _controller.ProcessRadioCall(radioCall);

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
                string response = _controller.ProcessRadioCall(radioCall);

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
                string response = _controller.ProcessRadioCall(radioCall);

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

                string response = _controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class PictureMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<IRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Id = "a1",
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<IRadioCall>();
                _mock.SetupGet(call => call.Intent).Returns("Picture");

            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderPictureCallsAreNotSupported()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Darkstar");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Darkstar");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Darkstar, we do not support picture calls.";
                string response = _controller.ProcessRadioCall(radioCall);

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
                string response = _controller.ProcessRadioCall(radioCall);

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
                string response = _controller.ProcessRadioCall(radioCall);

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

                string response = _controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }
        }

        [TestClass]
        public class UnknownMethod
        {
            private AwacsController _controller;
            private Player _sender;
            private Mock<IRadioCall> _mock;

            [TestInitialize]
            public void Init()
            {
                _controller = new AwacsController();

                _sender = new Player()
                {
                    Id = "a1",
                    Group = "dolt",
                    Flight = 1,
                    Plane = 2
                };

                _mock = new Mock<IRadioCall>();
            }

            [TestMethod]
            public void WhenNoAssignedAwacsCallSign_TellsTheSenderTransmissionCouldNotBeUnderstood()
            {
                _mock.SetupGet(call => call.Sender).Returns(_sender);
                _mock.SetupGet(call => call.ReceiverName).Returns("Darkstar");
                _mock.SetupGet(call => call.AwacsCallsign).Returns("Darkstar");

                var radioCall = _mock.Object;

                string expected = "dolt 1 2, Darkstar, I could not understand your transmission.";
                string response = _controller.ProcessRadioCall(radioCall);

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
                string response = _controller.ProcessRadioCall(radioCall);

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
                string response = _controller.ProcessRadioCall(radioCall);

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

                string response = _controller.ProcessRadioCall(radioCall);

                Assert.IsNull(response);
            }
        }
    }
}

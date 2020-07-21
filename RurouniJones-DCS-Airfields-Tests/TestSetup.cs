using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;

namespace RurouniJones.DCS.Airfields.Tests
{
    [TestClass]
    class TestSetup
    {
        [AssemblyInitialize()]
        public static void TestInitializer(TestContext _)
        {
            var config = new NLog.Config.LoggingConfiguration();

            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logconsole);

            // Apply config           
            LogManager.Configuration = config;

#pragma warning disable IDE0059 // Populate all the airfields before any tests are run.
                                // It removes initialization logs from the test output
            var airfields = Populator.Airfields;
#pragma warning restore IDE0059
        }
    }
}

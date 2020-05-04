using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    partial class GameQuerier
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static string ConnectionString()
        {
            var connectionString = $"Host={Settings.TAC_SCRIBE_HOST};Port={Settings.TAC_SCRIBE_PORT};Database={Settings.TAC_SCRIBE_DATABASE};" +
                                                 $"Username={Settings.TAC_SCRIBE_USERNAME};Password={Settings.TAC_SCRIBE_PASSWORD};";

            if (Settings.TAC_SCRIBE_FORCE_SSL == true) {
                connectionString += "sslmode=Require;";
            }
            return connectionString;
        }
    }
}

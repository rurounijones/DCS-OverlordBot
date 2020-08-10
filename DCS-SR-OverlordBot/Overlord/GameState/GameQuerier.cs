using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState
{
    partial class GameQuerier
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static string ConnectionString()
        {
            var connectionString = $"Host={Properties.Settings.Default.TacScribeHost};Port={Properties.Settings.Default.TacScribePort};Database={Properties.Settings.Default.TacScribeDatabase};" +
                                                 $"Username={Properties.Settings.Default.TacScribeUsername};Password={Properties.Settings.Default.TacScribePassword};";

            if (Properties.Settings.Default.TacScribeForceSSL)
            {
                connectionString += "sslmode=Require;";
            }
            return connectionString;
        }
    }
}

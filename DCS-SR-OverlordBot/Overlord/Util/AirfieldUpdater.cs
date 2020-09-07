using System.Linq;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Util
{
    internal class AirfieldUpdater
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void UpdateAirfields()
        {
            Logger.Debug("Updateing Airfields based on Game State");
            var airfields = Constants.Airfields; // These are airfields that have already had their navigation graphs setup

            var gameAirfields = GameQuerier.GetAirfields().Result;

            foreach (var gameAirfield in gameAirfields)
            {
                Logger.Debug($"Processing {gameAirfield.Name}");
                var airfield = airfields.FirstOrDefault(a => a.Name.Equals(gameAirfield.Name));

                if (airfield != null)
                {
                    // The following three fields are the ones that are dynamic and can change during a game session.
                    // Although wind is very unlikely to change enough to influence active runway settings.
                    airfield.Coalition = gameAirfield.Coalition;
                    airfield.WindHeading = airfield.WindHeading == -1 ? Properties.Settings.Default.WindHeading : gameAirfield.WindHeading;
                    airfield.WindSpeed = airfield.WindSpeed;
                    Logger.Debug($"Updated {gameAirfield.Name}");
                }
                else
                {
                    airfields.Add(gameAirfield);
                    Logger.Debug($"Added {gameAirfield.Name}");
                }
            }
        }
    }
}

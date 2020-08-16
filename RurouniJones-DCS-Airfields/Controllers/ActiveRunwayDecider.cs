using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;

namespace RurouniJones.DCS.Airfields.Controllers
{
    class ActiveRunwayDecider
    {

        /// <summary>
        /// Some Airfields will have special considerations. For example some will only switch end
        /// if the wind speed is high enough. For the moment we will stick with wind direction.
        /// </summary>
        /// <param name="Airfield"></param>
        /// <returns>A list of active runways</returns>
        public static List<Runway> GetActiveRunways(Airfield Airfield)
        {
            return GetActiveRunwaysByWind(Airfield);
        }

        private static List<Runway> GetActiveRunwaysByWind(Airfield Airfield)
        {
            return GetActiveRunwaysByHeading(Airfield);
        }

        private static List<Runway> GetActiveRunwaysByHeading(Airfield Airfield)
        {
            int desiredHeading = Airfield.WindHeading == -1 ? 90 : Airfield.WindHeading;
            var activeRunways = new List<Runway>();

            foreach (Runway runway in Airfield.Runways)
            {
                var runwayHeading = runway.Heading;
                if (desiredHeading - 90 < 0)
                {
                    desiredHeading += 360;
                    runwayHeading = runway.Heading + 360;
                }

                if (runwayHeading < desiredHeading + 90 && runwayHeading > desiredHeading - 90)
                {
                    activeRunways.Add(runway);
                }
            }
            return activeRunways;
        }
    }
}

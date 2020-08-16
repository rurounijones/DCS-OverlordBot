using System;
using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;

namespace RurouniJones.DCS.Airfields.Controllers
{
    internal class ActiveRunwayDecider
    {

        /// <summary>
        /// Some Airfields will have special considerations. For example some will only switch end
        /// if the wind speed is high enough. For the moment we will stick with wind direction.
        /// </summary>
        /// <param name="airfield"></param>
        /// <returns>A list of active runways</returns>
        public static List<Runway> GetActiveRunways(Airfield airfield)
        {

            var activeRunways = GetActiveRunwaysByWind(airfield);

            if (activeRunways.Count == 0 && airfield.Runways.Count > 0)
            {
                throw new NoActiveRunwaysFoundException($"Could not find active runways for {airfield.Name} with wind heading {airfield.WindHeading}");
            }
            return activeRunways;
        }

        private static List<Runway> GetActiveRunwaysByWind(Airfield airfield)
        {
            return GetActiveRunwaysByHeading(airfield);
        }

        private static List<Runway> GetActiveRunwaysByHeading(Airfield airfield)
        {
            var desiredHeading = airfield.WindHeading == -1 ? 90 : airfield.WindHeading;
            var activeRunways = new List<Runway>();

            foreach (var runway in airfield.Runways)
            {
                var runwayHeading = runway.Heading;
                if (desiredHeading - 90 < 0)
                {
                    desiredHeading += 360;
                    runwayHeading = runway.Heading + 360;
                }
                else if (desiredHeading < 360 && desiredHeading + 90 > 360 && runway.Heading < 90 )
                {
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
    public class NoActiveRunwaysFoundException : Exception
    {
        public NoActiveRunwaysFoundException(string message) : base(message)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using RurouniJones.DCS.Airfields.Structure;

namespace RurouniJones.DCS.Airfields.Controllers.Util
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
            var activeRunways = new List<Runway>();

            foreach (var runway in airfield.Runways)
            {
                var wH = airfield.WindHeading == -1 ? 90 : airfield.WindHeading;
                var rH = runway.Heading;

                if (Math.Min((wH - rH) < 0 ? wH - rH + 360 : wH - rH, (rH - wH) < 0 ? rH - wH + 360 : rH - wH) < 90)
                {
                    activeRunways.Add(runway);
                }
            }
            return activeRunways;
        }
    }
}
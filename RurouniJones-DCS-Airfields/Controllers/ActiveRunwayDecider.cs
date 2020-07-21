using RurouniJones.DCS.Airfields.Structure;
using System.Collections.Generic;

namespace RurouniJones.DCS.Airfields.Controllers
{
    class ActiveRunwayDecider
    {

        /// <summary>
        /// Some Airfields will have special considerations, right now we only care about the making sure
        /// the active runways are the one the graphs have been setup to use. Since the wind is always from
        /// the east we just need to make sure the Airfields that do not have eastward facing actives are
        /// specially handled
        /// </summary>
        /// <param name="Airfield"></param>
        /// <returns>A list of active runways</returns>
        public static List<Runway> GetActiveRunways(Airfield Airfield)
        {
            if (Airfield.Name.Equals("Al-Dhafra"))
            {
                return GetActiveRunwaysByHeading(Airfield, 310);
            }
            else if (Airfield.Name.Equals("Krasnodar-Center"))
            {
                return GetActiveRunwaysByHeading(Airfield, 270);
            }
            else if (Airfield.Name.Equals("Mineralnye Vody"))
            {
                return GetActiveRunwaysByHeading(Airfield, 120);
            }
            else
            {
                return GetActiveRunwaysByWind(Airfield);
            }
        }

        private static List<Runway> GetActiveRunwaysByWind(Airfield Airfield)
        {
            return GetActiveRunwaysByHeading(Airfield, Airfield.WindSource);
        }

        private static List<Runway> GetActiveRunwaysByHeading(Airfield Airfield, int heading)
        {
            var desiredHeading = heading + 360;
            var activeRunways = new List<Runway>();

            foreach (Runway runway in Airfield.Runways)
            {
                var runwayHeading = runway.Heading + 360;

                if (runwayHeading <= desiredHeading + 90 && runwayHeading >= desiredHeading - 90)
                {
                    activeRunways.Add(runway);
                }
            }
            return activeRunways;
        }
    }
}

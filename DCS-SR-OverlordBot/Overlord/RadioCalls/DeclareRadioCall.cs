using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls.LuisModels;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    class DeclareRadioCall : BaseRadioCall
    {
        /// <summary>
        /// The bearing given by the player, hopefully towards the target if the player is accurate
        /// </summary>
        public int BearingToTarget
        {
            get
            {
                // If no heading has been specified then we will assume the player is talking about a target
                // on their nose
                if (LuisResponse.Entities.Find(x => x.Role == "bearing") == null)
                {
                    return (int) Sender.Heading;
                }
                string bearingString = LuisResponse.Entities.Find(x => x.Role == "bearing").Entity;
                int.TryParse(bearingString, out int bearing);
                return bearing;
            }
        }

        public double DistanceToTarget
        {
            get
            {
                // If no distance is provided then we will assume a distance of one mile, which with the radius
                // also being one mile means a check from right in front of the caller out to 2 miles which is
                // probably enough for A-10s, Harriers and other visual only planes
                if (LuisResponse.Entities.Find(x => x.Role == "distance") == null)
                {
                    return 1;
                }
                string distanceString = LuisResponse.Entities.Find(x => x.Role == "distance").Entity;
                double.TryParse(distanceString, out double distance);
                return distance;
            }
        }

        public DeclareRadioCall(string luisResponse) : base(luisResponse) {}

        public DeclareRadioCall(BaseRadioCall baseRadioCall) : base(baseRadioCall) {}

    }
}

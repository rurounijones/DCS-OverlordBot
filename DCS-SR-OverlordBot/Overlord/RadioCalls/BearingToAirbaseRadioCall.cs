using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls.LuisModels;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    public class BearingToAirbaseRadioCall : BaseRadioCall
    {
        /// <summary>
        /// The normalized name of the Airbase we are looking for
        /// </summary>
        public string Airbase
        {
            get
            {
                if (LuisResponse.Entities.Find(x => x.Type == "airbase") == null)
                {
                    return null;
                }
                return LuisResponse.Entities.Find(x => x.Type == "airbase").Resolution.Values[0];
            }
        }

        public BearingToAirbaseRadioCall(string luisResponse) : base(luisResponse) { }
        public BearingToAirbaseRadioCall(BaseRadioCall baseRadioCall) : base(baseRadioCall) { }

    }
}

using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls.LuisModels;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    class BearingToFriendlyPlayerRadioCall : BaseRadioCall
    {
        /// <summary>
        /// The friendly player that the sender is trying to get a bearing to
        /// </summary>
        public Player FriendlyPlayer
        {
            get
            {

                var luisEntity = LuisResponse.CompositeEntities.Find(x => x.ParentType == "player_callsign");
                if (luisEntity == null)
                {
                    return null;
                }

                return BuildPlayer(luisEntity);
            }
        }
        public BearingToFriendlyPlayerRadioCall(string luisResponse) : base(luisResponse) {}
        public BearingToFriendlyPlayerRadioCall(BaseRadioCall baseRadioCall) : base(baseRadioCall) {}
    }
}

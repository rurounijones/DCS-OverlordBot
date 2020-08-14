using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    internal class BearingToFriendlyPlayerRadioCall : BaseRadioCall
    {
        /// <summary>
        /// The friendly player that the sender is trying to get a bearing to
        /// </summary>
        public Player FriendlyPlayer
        {
            get
            {

                var luisEntity = LuisResponse.CompositeEntities.Find(x => x.ParentType == "player_callsign");
                return luisEntity == null ? null : BuildPlayer(luisEntity);
            }
        }
        public BearingToFriendlyPlayerRadioCall(IRadioCall baseRadioCall) : base(baseRadioCall) { }
    }
}

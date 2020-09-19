using RurouniJones.DCS.OverlordBot.GameState;

namespace RurouniJones.DCS.OverlordBot.RadioCalls
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

                var compositeEntity = LuisResponse.CompositeEntities.Find(x => x.ParentType == "player_callsign");
                return compositeEntity == null ? null : BuildPlayer(compositeEntity, LuisResponse.Entities);
            }
        }
        public BearingToFriendlyPlayerRadioCall(IRadioCall baseRadioCall) : base(baseRadioCall) { }
    }
}

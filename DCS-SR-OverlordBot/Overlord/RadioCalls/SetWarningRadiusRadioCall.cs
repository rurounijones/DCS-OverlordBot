namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    internal class SetWarningRadiusRadioCall : BaseRadioCall
    {
        public int WarningRadius
        {
            get
            {
                if (LuisResponse.Entities.Find(x => x.Role == "distance") == null)
                {
                    return -1;
                }
                var distanceString = LuisResponse.Entities.Find(x => x.Role == "distance").Entity;
                int.TryParse(distanceString, out var distance);
                return distance;
            }
        }
        public SetWarningRadiusRadioCall(IRadioCall baseRadioCall) : base(baseRadioCall) { }
    }
}

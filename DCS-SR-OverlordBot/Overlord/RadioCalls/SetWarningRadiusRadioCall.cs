namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls
{
    class SetWarningRadiusRadioCall : BaseRadioCall
    {
        public int WarningRadius
        {
            get
            {
                if (LuisResponse.Entities.Find(x => x.Role == "distance") == null)
                {
                    return -1;
                }
                string distanceString = LuisResponse.Entities.Find(x => x.Role == "distance").Entity;
                int.TryParse(distanceString, out int distance);
                return distance;
            }
        }

        public SetWarningRadiusRadioCall(string luisResponse) : base(luisResponse) { }

        public SetWarningRadiusRadioCall(BaseRadioCall baseRadioCall) : base(baseRadioCall) { }
    }
}

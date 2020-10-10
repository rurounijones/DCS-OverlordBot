namespace RurouniJones.DCS.OverlordBot.RadioCalls
{
    class AtcRadioCall : BaseRadioCall
    {
        public string ControlName;

        public AtcRadioCall(string luisJson) : base(luisJson)
        {
            ControlName = LuisResponse.Entities
                .Find(x => x.Type.Equals("airbase_control_name")).Entity;
        }

    }
}

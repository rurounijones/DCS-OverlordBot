namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models
{
    public struct DCSLosCheckResult
    {
        public string id;
        public float los;

        public override string ToString()
        {
            return $"[id {id} LOS {los}]";
        }
    }
}
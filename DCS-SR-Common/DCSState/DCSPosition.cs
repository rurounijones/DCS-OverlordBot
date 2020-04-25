namespace Ciribob.DCS.SimpleRadio.Standalone.Common.DCSState
{
    public struct DcsPosition
    {
        public double x;
        public double y;
        public double z;

        public bool isValid()
        {
            return x != 0 && z != 0;
        }

        public override string ToString()
        {
            return $"Pos:[{x},{y},{z}]";
        }
    }
}
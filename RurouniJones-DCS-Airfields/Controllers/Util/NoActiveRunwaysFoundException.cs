using System;

namespace RurouniJones.DCS.Airfields.Controllers.Util
{
    public class NoActiveRunwaysFoundException : Exception
    {
        public NoActiveRunwaysFoundException(string message) : base(message)
        {
        }
    }
}

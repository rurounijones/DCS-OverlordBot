using System;

namespace RurouniJones.DCS.Airfields.Controllers
{
    public class TaxiPathNotFoundException : Exception
    {
        public TaxiPathNotFoundException()
        {
        }

        public TaxiPathNotFoundException(string message)
            : base(message)
        {
        }

        public TaxiPathNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}

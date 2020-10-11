using NLog;
using RurouniJones.DCS.Airfields.Structure;

namespace RurouniJones.DCS.Airfields.Controllers
{
    public abstract class BaseController
    {
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected readonly Airfield _airfield;

        protected BaseController(Airfield airfield)
        {
            _airfield = airfield;
        }
    }
}

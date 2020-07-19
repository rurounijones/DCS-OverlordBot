using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using RurouniJones.DCS.Airfields;
using RurouniJones.DCS.Airfields.Controllers;
using RurouniJones.DCS.Airfields.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class ReadytoTaxi
    {
        private static readonly List<Airfield> Airfields = Populator.Airfields;

        public static async Task<string> Process(string airbaseName, Player sender)
        {
            try
            {
                var airfield = Airfields.First(x => x.Name == airbaseName);
                return new GroundController(airfield).GetTaxiInstructions(sender.Position);
            } catch (InvalidOperationException)
            {
                return "There are no ATC services currently available at this airfield";
            }
        }
    }
}

using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using RurouniJones.DCS.Airfields;
using RurouniJones.DCS.Airfields.Controllers;
using RurouniJones.DCS.Airfields.Structure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    class ReadytoTaxi
    {
        private static readonly List<Airfield> Airfields = Populator.Airfields;

        private static readonly Array instructionsVariants = new ArrayList() { "", "taxi to", "proceed to", "head to" }.ToArray();
        private static readonly Array viaVariants = new ArrayList() { "via", "along", "using" }.ToArray();

        private static readonly Random randomizer = new Random();

        private static readonly TaxiInstructions DummyInstructions = new TaxiInstructions()
        {
            DestinationName = "Unknown Destination"
        };

        public static async Task<string> Process(BaseRadioCall radioCall)
        {
            TaxiInstructions taxiInstructions = DummyInstructions;
            Airfield airfield;
            try
            {
                airfield = Airfields.First(x => x.Name == radioCall.ReceiverName);
            }
            catch (InvalidOperationException)
            {
                return "There are no ATC services currently available at this airfield.";
            }
            try
            {
                taxiInstructions = new GroundController(airfield).GetTaxiInstructions(radioCall.Sender.Position);
                return ConvertTaxiInstructionsToSSML(taxiInstructions);
            }
            catch (TaxiPathNotFoundException)
            {
                return $"We could not find a path from your position to {taxiInstructions.DestinationName}.";
            }
        }

        private static string ConvertTaxiInstructionsToSSML(TaxiInstructions taxiInstructions)
        {
            string spokenInstructions = $"{Random(instructionsVariants)} {taxiInstructions.DestinationName} ";

            if (taxiInstructions.TaxiwayNames.Count > 0)
            {
                spokenInstructions += $"<break time=\"60ms\" /> {Random(viaVariants)}<break time=\"60ms\" /> {string.Join(" <break time=\"60ms\" /> ", taxiInstructions.TaxiwayNames)}";
            }

            if (taxiInstructions.Comments.Count > 0)
            {
                spokenInstructions += $", {string.Join(", ", taxiInstructions.Comments)}";
            }

            return spokenInstructions+ ".";
        }

        private static string Random(Array array)
        {
            return array.GetValue(randomizer.Next(array.Length)).ToString();
        }
    }
}

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.RadioCalls;
using NLog;
using RurouniJones.DCS.Airfields.Controllers;
using RurouniJones.DCS.Airfields.Structure;
using static Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Constants;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Intents
{
    internal class ReadytoTaxi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Array InstructionsVariants = new ArrayList { "", "taxi to", "proceed to", "head to" }.ToArray();
        private static readonly Array ViaVariants = new ArrayList { "via", "along", "using" }.ToArray();

        private static readonly Random Randomizer = new Random();

        private static readonly TaxiInstructions DummyInstructions = new TaxiInstructions
        {
            DestinationName = "Unknown Destination"
        };

        public static async Task<string> Process(IRadioCall radioCall)
        {
            var taxiInstructions = DummyInstructions;
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
                if (airfield.Runways.Count == 0)
                    return "There are no ATC services currently available at this airfield.";

                taxiInstructions = new GroundController(airfield).GetTaxiInstructions(radioCall.Sender.Position);
                return ConvertTaxiInstructionsToSsml(taxiInstructions);
            }
            catch (NoActiveRunwaysFoundException ex)
            {
                Logger.Error(ex, "No Active Runways found");
                return "We could not find any active runways.";
            }
            catch (TaxiPathNotFoundException ex)
            {
                Logger.Error(ex, "No Path found");
                return $"We could not find a path from your position to {taxiInstructions.DestinationName}.";
            }
        }

        private static string ConvertTaxiInstructionsToSsml(TaxiInstructions taxiInstructions)
        {
            var spokenInstructions = $"{Random(InstructionsVariants)} {taxiInstructions.DestinationName} ";

            if (taxiInstructions.TaxiwayNames.Count > 0)
            {
                spokenInstructions += $"<break time=\"60ms\" /> {Random(ViaVariants)}<break time=\"60ms\" /> {string.Join(" <break time=\"60ms\" /> ", taxiInstructions.TaxiwayNames)}";
            }

            if (taxiInstructions.Comments.Count > 0)
            {
                spokenInstructions += $", {string.Join(", ", taxiInstructions.Comments)}";
            }

            return spokenInstructions+ ".";
        }

        private static string Random(Array array)
        {
            return array.GetValue(Randomizer.Next(array.Length)).ToString();
        }
    }
}

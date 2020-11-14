using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Geo;
using NLog;
using RurouniJones.DCS.Airfields.Controllers.Approach;
using RurouniJones.DCS.Airfields.Controllers.Util;
using RurouniJones.DCS.Airfields.Structure;
using RurouniJones.DCS.OverlordBot.Controllers;
using RurouniJones.DCS.OverlordBot.RadioCalls;
using RurouniJones.DCS.OverlordBot.Util;
using Airfield = RurouniJones.DCS.OverlordBot.Models.Airfield;

namespace RurouniJones.DCS.OverlordBot.Intents
{
    internal class InboundToAirbase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task<string> Process(IRadioCall radioCall, string voice, ConcurrentQueue<byte[]> responseQueue)
        {
            Airfield airfield;
            try
            {
                airfield = Constants.Airfields.First(x => x.Name == radioCall.ReceiverName);
                var approachRoute = new ApproachController(airfield).GetApproachRoute(radioCall.Sender.Position);

                var initialTrueBearing = Geospatial.BearingTo(radioCall.Sender.Position.Coordinate,
                    new Coordinate(approachRoute.First().Latitude, approachRoute.First().Longitude));

                var initialMagneticBearing =
                    Regex.Replace(Geospatial.TrueToMagnetic(radioCall.Sender.Position, initialTrueBearing).ToString("000"), "\\d{1}", " $0");

                var response = $"fly heading {initialMagneticBearing}, descend and maintain 2,000, reduce speed 2 0 0 knots, for vectors to {approachRoute.Last().Name}, {approachRoute.First().Name}";

                var currentPosition = new NavigationPoint {
                    Name = "Current Position",
                    Latitude = radioCall.Sender.Position.Coordinate.Latitude,
                    Longitude = radioCall.Sender.Position.Coordinate.Longitude
                };
                
                approachRoute = approachRoute.Prepend(currentPosition).ToList();

                new AtcProgressChecker(radioCall.Sender, airfield, voice, approachRoute, responseQueue);

                return response;
            }
            catch (InvalidOperationException)
            {
                return "There are no ATC services currently available at this airfield.";
            }
            catch (NoActiveRunwaysFoundException ex)
            {
                Logger.Error(ex, "No Active Runways found");
                return "We could not find any active runways.";
            }
        }
    }
}

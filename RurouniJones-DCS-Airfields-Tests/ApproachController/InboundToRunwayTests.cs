using System;
using System.Collections.Generic;
using System.Linq;
using Geo.Geometries;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RurouniJones.DCS.Airfields.Controllers.Approach;

namespace RurouniJones.DCS.Airfields.Controllers.Tests
{
    [TestClass]
    public class InboundToRunwayTests
    {
        public static IEnumerable<object[]> ApproachScenarios()
        {
            yield return new object[] { new ApproachScenario("Anapa-Vityazevo", 090, new Point(44.0101581, 37.3481765), new List<string>{"Entry South", "Initial-0 4", "Runway-0 4"}) };
            yield return new object[] { new ApproachScenario("Anapa-Vityazevo", 090, new Point(46.0101581, 37.3481765), new List<string>{"Entry-Exit West", "Initial-0 4", "Runway-0 4"}) };
            //yield return new object[] { new ApproachScenario("Anapa-Vityazevo", 270, new Point(44.0101581, 37.3481765), new List<string>{"Entry-Exit East", "Initial-2 2", "Runway-2 2"}) };
            //yield return new object[] { new ApproachScenario("Anapa-Vityazevo", 270, new Point(46.0101581, 37.3481765), new List<string>{"Entry-Exit North", "Initial-2 2", "Runway-2 2"}) };
        }

        [DataTestMethod]
        [DynamicData(nameof(ApproachScenarios), DynamicDataSourceType.Method)]
        public void GetApproachRoute(ApproachScenario scenario)
        {
            var airfield = Populator.Airfields.First(af => af.Name.Equals(scenario.Airfield));
            var controller = new ApproachController(airfield);

            airfield.WindHeading = scenario.Wind;

            var expected = scenario.Waypoints;

            var actual = controller.GetApproachRoute(scenario.StartPoint).Select(x => x.Name).ToList();

            Console.WriteLine(string.Join(" -> ", actual));

            CollectionAssert.AreEqual(expected, actual);
        }

        public class ApproachScenario
        {
            public string Airfield { get; }
            public int Wind { get; }
            public Point StartPoint { get; }
            public List<string> Waypoints;

            public ApproachScenario(string airfield, int wind, Point startPoint, List<string> wayPoints)
            {
                Airfield = airfield;
                Wind = wind;
                StartPoint = startPoint;
                Waypoints = wayPoints;
            }
            
            public override string ToString()
            {
                return $"{Airfield} (Wind {Wind:D3}): {Waypoints.First()} -> {Waypoints.Last()}";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RurouniJones.DCS.Airfields;
using RurouniJones.DCS.OverlordBot.Models;

namespace RurouniJones.DCS.OverlordBot
{
    public static class Constants
    {
        public static readonly List<Airfield> Airfields = Populator.Airfields.Cast<Airfield>().ToList();

        public static ActivitySource ActivitySource = new ActivitySource(
            $"OverlordBot {Properties.Settings.Default.ServerShortName}",
            "Development");
    }

    public enum Coalition
    {
        Neutral,
        Redfor,
        Bluefor
    }

    internal static class CoalitionMethods
    {

        public static Coalition GetOpposingCoalition(this Coalition coalition)
        {
            switch (coalition)
            {
                case Coalition.Redfor:
                    return Coalition.Bluefor;
                case Coalition.Bluefor:
                    return Coalition.Redfor;
                default:
                    throw new ArgumentException($"Cannot determine opposing coalition for {coalition}");
            }
        }
    }
}

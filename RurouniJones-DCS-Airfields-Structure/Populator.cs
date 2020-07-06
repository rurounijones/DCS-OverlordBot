using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace RurouniJones.DCS.Airfields.Structure
{
    public class Populator
    {
        public static readonly List<Airfield> Airfields = PopulateAirfields();

        private static List<Airfield> PopulateAirfields()
        {
            List<Airfield> airfields = new List<Airfield>();

            string[] fileArray = Directory.GetFiles("Data/", "*.json");

            foreach (string file in fileArray)
            {
                airfields.Add(JsonConvert.DeserializeObject<Airfield>(File.ReadAllText(file)));
            }

            return airfields;
        }
    }
}

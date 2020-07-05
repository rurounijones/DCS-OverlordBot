using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Navigation;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc
{
    class Manager
    {
        public static readonly List<Airfield> Airfields = PopulateAirfields();

        private static volatile Manager _instance;
        private static object _lock = new object();

        private Manager() { }

        public static Manager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new Manager();
                    }
                }

                return _instance;
            }
        }

        private static List<Airfield> PopulateAirfields()
        {
            List<Airfield> airfields = new List<Airfield>();

            string[] fileArray = Directory.GetFiles("Overlord/Data/Airfields/", "*.json");

            foreach (string file in fileArray)
            {
                airfields.Add(JsonConvert.DeserializeObject<Airfield>(File.ReadAllText(file)));
            }

            return airfields;
        }
    }
}

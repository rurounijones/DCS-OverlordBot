using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.GameState;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput
{
    public static class AircraftReportingNamePronouncer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static readonly List<Aircraft> AircraftMapping = JsonConvert.DeserializeObject<List<Aircraft>>(File.ReadAllText("Overlord/Data/Aircraft.json"));

        public static string PronounceName(Contact contact)
        {
            Aircraft aircraft;
            try
            {
                if (contact.Name == null || contact.Name.Length == 0)
                {
                    return "unknown";
                }
                aircraft = AircraftMapping.FirstOrDefault(ac => ac.DcsId.Equals(contact.Name));
                if (aircraft != null && aircraft.NatoName != null && aircraft.NatoName.Length > 0)
                {
                    return aircraft.NatoName;
                }
                else
                {
                    return contact.Name;
                }
            }
            catch (NullReferenceException ex)
            {
                Logger.Error(ex, "Exception pronouncing name of contact");
                return "unknown";
            }
        }

        public class Aircraft
        {
            [JsonProperty(PropertyName = "dcs_id")]
            public string DcsId { get; set; }

            [JsonProperty(PropertyName = "nato_name")]
            public string NatoName { get; set; }
        }
    }
}

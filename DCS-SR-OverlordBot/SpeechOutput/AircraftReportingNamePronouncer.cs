using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using RurouniJones.DCS.OverlordBot.GameState;

namespace RurouniJones.DCS.OverlordBot.SpeechOutput
{
    public static class AircraftReportingNamePronouncer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static readonly List<Aircraft> AircraftMapping = JsonConvert.DeserializeObject<List<Aircraft>>(File.ReadAllText("Data/Aircraft.json"));

        public static string PronounceName(Contact contact)
        {
            try
            {
                if (string.IsNullOrEmpty(contact.Name))
                {
                    return "unknown";
                }
                var aircraft = AircraftMapping.FirstOrDefault(ac => ac.DcsId.Equals(contact.Name));
                if (aircraft?.NatoName != null && aircraft.NatoName.Length > 0)
                {
                    return aircraft.NatoName;
                }
                return contact.Name;
            }
            catch (NullReferenceException ex)
            {
                Logger.Error(ex, $"Exception pronouncing name of contact for {contact.Name}");
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

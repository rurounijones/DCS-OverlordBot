using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
{
    public class SyncedServerSettings
    {
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static SyncedServerSettings _instance;
        private static readonly object Lock = new object();
        private static readonly Dictionary<string, string> Defaults = DefaultServerSettings.Defaults;

        private readonly ConcurrentDictionary<string, string> _settings;

        public List<double> GlobalFrequencies { get; set; } = new List<double>();

        public SyncedServerSettings()
        {
            _settings = new ConcurrentDictionary<string, string>();
        }

        public static SyncedServerSettings Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new SyncedServerSettings();
                    }
                }
                return _instance;
            }
        }

        public string GetSetting(ServerSettingsKeys key)
        {
            var setting = key.ToString();

            return _settings.GetOrAdd(setting, Defaults.ContainsKey(setting) ? Defaults[setting] : "");
        }

        public bool GetSettingAsBool(ServerSettingsKeys key)
        {
            return Convert.ToBoolean(GetSetting(key));
        }

        public void Decode(Dictionary<string, string> encoded)
        {
            foreach (var kvp in encoded)
            {
                _settings.AddOrUpdate(kvp.Key, kvp.Value, (key, oldVal) => kvp.Value);

                if (!kvp.Key.Equals(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString())) continue;
                var freqStringList = kvp.Value.Split(',');

                var newList = new List<double>();
                foreach (var freq in freqStringList)
                {
                    if (!double.TryParse(freq.Trim(), out var freqDouble)) continue;
                    freqDouble *= 1e+6; //convert to Hz from MHz
                    newList.Add(freqDouble);
                    _logger.Debug("Adding Server Global Frequency: " + freqDouble);
                }

                GlobalFrequencies = newList;
            }
        }
    }
}
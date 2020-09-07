using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using NLog;
using SharpConfig;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings
{
    public enum SettingsKeys
    {
        //settings
        RadioEffects = 0,
        RadioSwitchIsPtt = 4,

        RadioEncryptionEffects = 8, //Radio Encryption effects
        AutoConnectPrompt = 10, //message about auto connect
        RadioOverlayTaskbarHide = 11,

        RefocusDcs = 19,
        ExpandControls = 20,

        RadioEffectsClipping = 21,

        MinimiseToTray = 22,
        StartMinimised = 23,

        RadioRxEffectsStart = 40, // Recieving Radio Effects
        RadioRxEffectsEnd = 41,
        RadioTxEffectsStart = 42, // Recieving Radio Effects
        RadioTxEffectsEnd = 43,

        AutoSelectPresetChannel = 44, //auto select preset channel

        AudioInputDeviceId = 45,
        AudioOutputDeviceId = 46,
        LastServer = 47,
        MicBoost = 48,
        SpeakerBoost = 49,
        RadioX = 50,
        RadioY = 51,
        RadioSize = 52,
        RadioOpacity = 53,
        RadioWidth = 54,
        RadioHeight = 55,
        ClientX = 56,
        ClientY = 57,
        AwacsX = 58,
        AwacsY = 59,
        MicAudioOutputDeviceId = 60,


        CliendIdShort = 61,
        ClientIdLong = 62,
        DcslosOutgoingUdp = 63, //9086
        DcsIncomingUdp = 64, //9084
        CommandListenerUdp = 65, //=9040,
        OutgoingDcsudpInfo = 66, //7080
        OutgoingDcsudpOther = 67, //7082
        DcsIncomingGameGuiudp = 68, // 5068
        DcslosIncomingUdp = 69, //9085

        AGC = 70,
        AgcTarget = 71,
        AgcDecrement=72,
        AgcLevelMax = 73,

        Denoise=74,
        DenoiseAttenuation = 75,

        AlwaysAllowHotasControls = 76,
        AllowDcsptt = 77,

        LastSeenName = 78,

        CheckForBetaUpdates = 79,

        AllowMultipleInstances = 80, // Allow for more than one SRS instance to be ran simultaneously. Config-file only!

        AutoConnectMismatchPrompt = 81, //message about auto connect mismatch

        DisableWindowVisibilityCheck = 82,
        PlayConnectionSounds = 83
    }

    public class SettingsStore
    {
        private const string CfgFileName = "client.cfg";

        private static readonly object Lock = new object();

        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly Configuration _configuration;

        private readonly string _cfgFile = CfgFileName;

        private SettingsStore()
        {
            //check commandline
            var args = Environment.GetCommandLineArgs();

            foreach (var arg in args)
            {
                if (arg.StartsWith("-cfg="))
                {
                    _cfgFile = arg.Replace("-cfg=", "").Trim();
                }
            }

            try
            {
                _configuration = Configuration.LoadFromFile(_cfgFile);
            }
            catch (FileNotFoundException)
            {
                _logger.Info($"Did not find client config file at path ${_cfgFile}, initialising with default config");

                _configuration = new Configuration
                {
                    new Section("Position Settings"),
                    new Section("Client Settings"),
                    new Section("Network Settings")
                };

                Save();
            }
            catch (ParserException ex)
            {
                _logger.Error(ex, "Failed to parse client config, potentially corrupted. Creating backing and re-initialising with default config");

                MessageBox.Show("Failed to read client config, it might have become corrupted.\n" +
                    "SRS will create a backup of your current config file (client.cfg.bak) and initialise using default settings.",
                    "Config error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                try
                {
                    File.Copy(_cfgFile, _cfgFile+".bak", true);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to create backup of corrupted config file, ignoring");
                }

                _configuration = new Configuration
                {
                    new Section("Position Settings"),
                    new Section("Client Settings"),
                    new Section("Network Settings")
                };

                Save();
            }
        }

        public static SettingsStore Instance => _instance ?? (_instance = new SettingsStore());

        private readonly Dictionary<string, string> _defaultSettings = new Dictionary<string, string>
        {
            {SettingsKeys.RadioEffects.ToString(), "true"},
            {SettingsKeys.RadioEffectsClipping.ToString(), "true"},
            {SettingsKeys.RadioSwitchIsPtt.ToString(), "false"},

            {SettingsKeys.RadioEncryptionEffects.ToString(), "true"},
            {SettingsKeys.AutoConnectPrompt.ToString(), "false"},
            {SettingsKeys.AutoConnectMismatchPrompt.ToString(), "true"},
            {SettingsKeys.RadioOverlayTaskbarHide.ToString(), "false"},
            {SettingsKeys.RefocusDcs.ToString(), "false"},
            {SettingsKeys.ExpandControls.ToString(), "false"},

            {SettingsKeys.MinimiseToTray.ToString(), "false"},
            {SettingsKeys.StartMinimised.ToString(), "false"},

            {SettingsKeys.RadioRxEffectsStart.ToString(), "true"},
            {SettingsKeys.RadioRxEffectsEnd.ToString(), "true"},
            {SettingsKeys.RadioTxEffectsStart.ToString(), "true"},
            {SettingsKeys.RadioTxEffectsEnd.ToString(), "true"},

            {SettingsKeys.AutoSelectPresetChannel.ToString(), "true"},

            {SettingsKeys.AudioInputDeviceId.ToString(), ""},
            {SettingsKeys.AudioOutputDeviceId.ToString(), ""},
            {SettingsKeys.MicAudioOutputDeviceId.ToString(), ""},

            {SettingsKeys.LastServer.ToString(), "127.0.0.1"},

            {SettingsKeys.MicBoost.ToString(), "0.514"},
            {SettingsKeys.SpeakerBoost.ToString(), "0.514"},

            {SettingsKeys.RadioX.ToString(), "300"},
            {SettingsKeys.RadioY.ToString(), "300"},
            {SettingsKeys.RadioSize.ToString(), "1.0"},
            {SettingsKeys.RadioOpacity.ToString(), "1.0"},

            {SettingsKeys.RadioWidth.ToString(), "122"},
            {SettingsKeys.RadioHeight.ToString(), "270"},

            {SettingsKeys.ClientX.ToString(), "200"},
            {SettingsKeys.ClientY.ToString(), "200"},

            {SettingsKeys.AwacsX.ToString(), "300"},
            {SettingsKeys.AwacsY.ToString(), "300"},

            {SettingsKeys.CliendIdShort.ToString(), ShortGuid.NewGuid().ToString()},
            {SettingsKeys.ClientIdLong.ToString(), Guid.NewGuid().ToString()},

            {SettingsKeys.DcslosOutgoingUdp.ToString(), "9086"},
            {SettingsKeys.DcsIncomingUdp.ToString(), "9084"},
            {SettingsKeys.CommandListenerUdp.ToString(), "9040"},
            {SettingsKeys.OutgoingDcsudpInfo.ToString(), "7080"},
            {SettingsKeys.OutgoingDcsudpOther.ToString(), "7082"},
            {SettingsKeys.DcsIncomingGameGuiudp.ToString(), "5068"},
            {SettingsKeys.DcslosIncomingUdp.ToString(), "9085"},

            {SettingsKeys.AGC.ToString(), "true"},
            {SettingsKeys.AgcTarget.ToString(), "30000"},
            {SettingsKeys.AgcDecrement.ToString(), "-60"},
            {SettingsKeys.AgcLevelMax.ToString(),"68" },

            {SettingsKeys.Denoise.ToString(),"true" },
            {SettingsKeys.DenoiseAttenuation.ToString(),"-30" },

            {SettingsKeys.AlwaysAllowHotasControls.ToString(),"false" },
            {SettingsKeys.AllowDcsptt.ToString(),"true" },

            {SettingsKeys.LastSeenName.ToString(), ""},

            {SettingsKeys.CheckForBetaUpdates.ToString(), "false"},

            {SettingsKeys.AllowMultipleInstances.ToString(), "false"},

            {SettingsKeys.DisableWindowVisibilityCheck.ToString(), "false"},
            {SettingsKeys.PlayConnectionSounds.ToString(), "true"}
        };

        public Setting GetPositionSetting(SettingsKeys key)
        {
            return GetSetting("Position Settings", key.ToString());
        }

        public void SetPositionSetting(SettingsKeys key, double value)
        {
            SetSetting("Position Settings", key.ToString(), value.ToString(CultureInfo.InvariantCulture));
        }

        public Setting GetClientSetting(SettingsKeys key)
        {
            return GetSetting("Client Settings", key.ToString());
        }

        public void SetClientSetting(SettingsKeys key, string value)
        {
            SetSetting("Client Settings", key.ToString(), value);
        }

        private Setting GetSetting(string section, string setting)
        {
            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (_configuration[section].Contains(setting)) return _configuration[section][setting];
            if (_defaultSettings.ContainsKey(setting))
            {
                //save
                _configuration[section]
                    .Add(new Setting(setting, _defaultSettings[setting]));

                Save();
            }
            else
            {
                _configuration[section]
                    .Add(new Setting(setting, ""));
                Save();
            }

            return _configuration[section][setting];
        }

        private void SetSetting(string section, string key, string setting)
        {
            if (setting == null)
            {
                setting = "";
            }
            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (!_configuration[section].Contains(key))
            {
                _configuration[section].Add(new Setting(key, setting));
            }
            else
            {
                _configuration[section][key].StringValue = setting;
            }

            Save();
        }

        private static SettingsStore _instance;

        public void Save()
        {
            lock (Lock)
            {
                try
                {
                    _configuration.SaveToFile(_cfgFile);
                }
                catch (Exception)
                {
                    _logger.Error("Unable to save settings!");
                }
            }
        }
    }
}
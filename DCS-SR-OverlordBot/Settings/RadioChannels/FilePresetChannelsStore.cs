using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels
{
    public class FilePresetChannelsStore : IPresetChannelsStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public IEnumerable<PresetChannel> LoadFromStore(string radioName)
        {
            var file = FindRadioFile(NormalizeString(radioName));

            return file != null ? ReadFrequenciesFromFile(file) : new List<PresetChannel>();
        }

        private static List<PresetChannel> ReadFrequenciesFromFile(string filePath)
        {
            var channels = new List<PresetChannel>();
            var lines = File.ReadAllLines(filePath);

            const double mHz = 1000000;
            if (lines.Length <= 0) return channels;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length <= 0) continue;
                try
                {
                    var frequency = double.Parse(trimmed, CultureInfo.InvariantCulture);
                    channels.Add(new PresetChannel
                    {
                        Text = trimmed,
                        Value = frequency * mHz
                    });
                }
                catch (Exception)
                {
                    Logger.Log(LogLevel.Info, "Error parsing frequency  ");
                }
            }

            return channels;
        }

        private static string FindRadioFile(string radioName)
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory);

            return (from fileAndPath in files let name = Path.GetFileNameWithoutExtension(fileAndPath) where NormalizeString(name) == radioName select fileAndPath).FirstOrDefault();
        }

        private static string NormalizeString(string str)
        {
            //only allow alphanumeric, remove all spaces etc
            return Regex.Replace(str, "[^a-zA-Z0-9]", "").ToLower();
        }
    }
}
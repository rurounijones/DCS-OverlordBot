using System.Collections.Generic;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels
{
    public interface IPresetChannelsStore
    {
        IEnumerable<PresetChannel> LoadFromStore(string radioName);
    }
}
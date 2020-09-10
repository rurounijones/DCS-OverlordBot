using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class PresetChannelsViewModel
    {
        private readonly IPresetChannelsStore _channelsStore;
        private int _radioId;

        public DelegateCommand DropDownClosedCommand { get; set; }

        private readonly Network.Client _client;

        private readonly object _presetChannelLock = new object();
        private ObservableCollection<PresetChannel> _presetChannels;

        public ObservableCollection<PresetChannel> PresetChannels
        {
            get => _presetChannels;
            set
            {
                _presetChannels = value;
                BindingOperations.EnableCollectionSynchronization(_presetChannels, _presetChannelLock);
            }
        }

        public PresetChannelsViewModel(IPresetChannelsStore channels, int radioId, Network.Client client)
        {
            _client = client;
            _radioId = radioId;
            _channelsStore = channels;
            ReloadCommand = new DelegateCommand(OnReload);
            DropDownClosedCommand = new DelegateCommand(DropDownClosed);
            PresetChannels = new ObservableCollection<PresetChannel>();
        }


        public ICommand ReloadCommand { get; }

        private void DropDownClosed(object args)
        {
        }

        public PresetChannel SelectedPresetChannel { get; set; }

        public double Max { get; set; }
        public double Min { get; set; }

        public void Reload()
        {
            PresetChannels.Clear();

            var radios = _client.DcsPlayerRadioInfo.radios;

            var radio = radios[_radioId];

            var i = 1;
            foreach (var channel in _channelsStore.LoadFromStore(radio.name))
            {
                if (!((double) channel.Value < Max) || !((double) channel.Value > Min)) continue;
                channel.Channel = i++;
                PresetChannels.Add(channel);
            }
        }

        private void OnReload()
        {
            Reload();
        }

        public void Clear()
        {
            PresetChannels.Clear();
        }
    }
}
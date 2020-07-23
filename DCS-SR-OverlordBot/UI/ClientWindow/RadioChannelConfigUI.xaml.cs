using System.Windows;
using System.Windows.Controls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for RadioChannelConfigUI.xaml
    /// </summary>
    public partial class RadioChannelConfigUi : UserControl
    {
        public RadioChannelConfigUi()
        {
            InitializeComponent();

            //I do this because at this point SettingConfig hasn't been set
            //but it has when this is called
            ChannelSelector.Loaded += InitComboBox;
        }

        public SettingsKeys SettingConfig { get; set; }

        private void InitComboBox(object sender, RoutedEventArgs e)
        {
            //cannot be inlined or it causes an issue
            var value = SettingsStore.Instance.GetClientSetting(SettingConfig).StringValue;

            ChannelSelector.SelectedValue = value;

            ChannelSelector.SelectionChanged += ChannelSelector_SelectionChanged;
        }

        private void ChannelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (string) ChannelSelector.SelectedValue;

            SettingsStore.Instance.SetClientSetting(SettingConfig, selected);
        }
    }
}
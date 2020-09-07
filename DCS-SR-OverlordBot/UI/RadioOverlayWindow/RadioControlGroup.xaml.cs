using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow.PresetChannels;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using Ciribob.DCS.SimpleRadio.Standalone.Common;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow
{
    /// <summary>
    ///     Interaction logic for RadioControlGroup.xaml
    /// </summary>
    public partial class RadioControlGroup
    {
        private const double MHz = 1000000;
        private bool _dragging;
        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        public PresetChannelsViewModel ChannelViewModel { get; set; }

        public RadioControlGroup()
        {
            DataContext = this; // set data context

            InitializeComponent();
        }

        private int _radioId;

        public int RadioId
        {
            private get { return _radioId; }
            set
            {
                _radioId = value;
                UpdateBinding();
            }
        }

        //updates the binding so the changes are picked up for the linked FixedChannelsModel
        private void UpdateBinding()
        {
            ChannelViewModel = _clientStateSingleton.FixedChannels[_radioId - 1];

            var bindingExpression = PresetChannelsView.GetBindingExpression(DataContextProperty);
            bindingExpression?.UpdateTarget();
        }

        private void Up0001_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(0.001, RadioId);
        }

        private void Up001_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(0.01, RadioId);
        }

        private void Up01_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(0.1, RadioId);
        }

        private void Up1_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(1, RadioId);
        }

        private void Up10_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(10, RadioId);
        }

        private void Down10_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(-10, RadioId);
        }

        private void Down1_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(-1, RadioId);
        }

        private void Down01_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(-0.1, RadioId);
        }

        private void Down001_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(-0.01, RadioId);
        }

        private void Down0001_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.UpdateRadioFrequency(-0.001, RadioId);
        }

        private void RadioSelectSwitch(object sender, RoutedEventArgs e)
        {
            RadioHelper.SelectRadio(RadioId);
        }

        private void RadioFrequencyText_Click(object sender, MouseButtonEventArgs e)
        {
            RadioHelper.SelectRadio(RadioId);
        }

        private void RadioFrequencyText_RightClick(object sender, MouseButtonEventArgs e)
        {
            RadioHelper.ToggleGuard(RadioId);
        }

        private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
        {
            _dragging = true;
        }


        private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
        {
            var currentRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

            if (currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {
                var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[RadioId];

                clientRadio.volume = (float) RadioVolume.Value / 100.0f;
            }

            _dragging = false;
        }

        private void ToggleButtons(bool enable)
        {
            if (enable)
            {
                Up10.Visibility = Visibility.Visible;
                Up1.Visibility = Visibility.Visible;
                Up01.Visibility = Visibility.Visible;
                Up001.Visibility = Visibility.Visible;
                Up0001.Visibility = Visibility.Visible;

                Down10.Visibility = Visibility.Visible;
                Down1.Visibility = Visibility.Visible;
                Down01.Visibility = Visibility.Visible;
                Down001.Visibility = Visibility.Visible;
                Down0001.Visibility = Visibility.Visible;

                Up10.IsEnabled = true;
                Up1.IsEnabled = true;
                Up01.IsEnabled = true;
                Up001.IsEnabled = true;
                Up0001.IsEnabled = true;

                Down10.IsEnabled = true;
                Down1.IsEnabled = true;
                Down01.IsEnabled = true;
                Down001.IsEnabled = true;
                Down0001.IsEnabled = true;

                //  ReloadButton.IsEnabled = true;
                //LoadFromFileButton.IsEnabled = true;

                PresetChannelsView.IsEnabled = true;
            }
            else
            {
                Up10.Visibility = Visibility.Hidden;
                Up1.Visibility = Visibility.Hidden;
                Up01.Visibility = Visibility.Hidden;
                Up001.Visibility = Visibility.Hidden;
                Up0001.Visibility = Visibility.Hidden;

                Down10.Visibility = Visibility.Hidden;
                Down1.Visibility = Visibility.Hidden;
                Down01.Visibility = Visibility.Hidden;
                Down001.Visibility = Visibility.Hidden;
                Down0001.Visibility = Visibility.Hidden;

                PresetChannelsView.IsEnabled = false;
            }
        }

        internal void RepaintRadioStatus()
        {
            SetupEncryption();

            var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo == null || !dcsPlayerRadioInfo.IsCurrent())
            {
                RadioActive.Fill = new SolidColorBrush(Colors.Red);
                RadioLabel.Text = "No Radio";
                RadioFrequency.Text = "Unknown";

                RadioVolume.IsEnabled = false;

                ToggleButtons(false);

                //reset dragging just incase
                _dragging = false;
            }
            else
            {
                if (RadioId == dcsPlayerRadioInfo.selected)
                {
                    var transmitting = UdpVoiceHandler.RadioSendingState;

                    if (transmitting.IsSending && transmitting.SendingOn == RadioId)
                    {
                        RadioActive.Fill = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#96FF6D"));
                    }
                    else
                    {
                        RadioActive.Fill = new SolidColorBrush(Colors.Green);
                    }
                }
                else
                {
                    RadioActive.Fill = new SolidColorBrush(Colors.Orange);
                }

                var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

                switch (currentRadio.modulation)
                {
                    // disabled
                    case RadioInformation.Modulation.DISABLED:
                        RadioActive.Fill = new SolidColorBrush(Colors.Red);
                        RadioLabel.Text = "No Radio";
                        RadioFrequency.Text = "Unknown";

                        RadioVolume.IsEnabled = false;

                        ToggleButtons(false);
                        return;
                    //intercom
                    case RadioInformation.Modulation.INTERCOM:
                        RadioFrequency.Text = "INTERCOM";
                        break;
                    default:
                    {
                        RadioFrequency.Text =
                            (currentRadio.freq / MHz).ToString("0.000",
                                CultureInfo.InvariantCulture) + //make nuber UK / US style with decimals not commas!
                            (currentRadio.modulation == 0 ? "AM" : "FM");
                        if (currentRadio.secFreq > 100)
                        {
                            RadioFrequency.Text += " G";
                        }

                        if (currentRadio.channel >= 0)
                        {
                            RadioFrequency.Text += " C" + currentRadio.channel;
                        }


                        if (currentRadio.enc && currentRadio.encKey > 0)
                        {
                            RadioFrequency.Text += " E" + currentRadio.encKey; // ENCRYPTED
                        }

                        break;
                    }
                }


                RadioLabel.Text = dcsPlayerRadioInfo.radios[RadioId].name;

                RadioVolume.IsEnabled = currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY;

                ToggleButtons(currentRadio.freqMode == RadioInformation.FreqMode.OVERLAY);

                if (_dragging == false)
                {
                    RadioVolume.Value = currentRadio.volume * 100.0;
                }
            }
        }

        private void SetupEncryption()
        {
            var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

            if (dcsPlayerRadioInfo != null || dcsPlayerRadioInfo.IsCurrent())
            {
                var currentRadio = dcsPlayerRadioInfo.radios[RadioId];

                EncryptionKeySpinner.Value = currentRadio.encKey;

                //update stuff
                if (currentRadio.encMode == RadioInformation.EncryptionMode.NO_ENCRYPTION
                    || currentRadio.encMode == RadioInformation.EncryptionMode.ENCRYPTION_FULL
                    || currentRadio.modulation == RadioInformation.Modulation.INTERCOM)
                {
                    //Disable everything
                    EncryptionKeySpinner.IsEnabled = false;
                    EncryptionButton.IsEnabled = false;
                    EncryptionButton.Content = "Enable";
                }
                else switch (currentRadio.encMode)
                {
                    case RadioInformation.EncryptionMode.ENCRYPTION_COCKPIT_TOGGLE_OVERLAY_CODE:
                        //allow spinner
                        EncryptionKeySpinner.IsEnabled = true;

                        //disallow encryption toggle
                        EncryptionButton.IsEnabled = false;
                        EncryptionButton.Content = "Enable";
                        break;
                    case RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY:
                        EncryptionKeySpinner.IsEnabled = true;
                        EncryptionButton.IsEnabled = true;

                        EncryptionButton.Content = currentRadio.enc ? "Disable" : "Enable";
                        break;
                }
            }
            else
            {
                //Disable everything
                EncryptionKeySpinner.IsEnabled = false;
                EncryptionButton.IsEnabled = false;
                EncryptionButton.Content = "Enable";
            }
        }


        internal void RepaintRadioReceive()
        {
            var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
            if (dcsPlayerRadioInfo == null)
            {
                RadioFrequency.Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
            }
            else
            {
                var receiveState = UdpVoiceHandler.RadioReceivingState[RadioId];
                //check if current

                if (receiveState == null || !receiveState.IsReceiving)
                {
                    RadioFrequency.Foreground =
                        new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
                }
                else if (receiveState.IsReceiving)
                {
                    RadioFrequency.Foreground = receiveState.IsSecondary ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.White);
                }
                else
                {
                    RadioFrequency.Foreground =
                        new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
                }
            }
        }


        private void Encryption_ButtonClick(object sender, RoutedEventArgs e)
        {
            var currentRadio = RadioHelper.GetRadio(RadioId);

            if (currentRadio == null || currentRadio.modulation == RadioInformation.Modulation.DISABLED) return;
            //update stuff
            if (currentRadio.encMode != RadioInformation.EncryptionMode.ENCRYPTION_JUST_OVERLAY) return;
            RadioHelper.ToggleEncryption(RadioId);

            EncryptionButton.Content = currentRadio.enc ? "Enable" : "Disable";
        }

        private void EncryptionKeySpinner_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (EncryptionKeySpinner?.Value != null)
                RadioHelper.SetEncryptionKey(RadioId, (byte) EncryptionKeySpinner.Value);
        }
    }
}
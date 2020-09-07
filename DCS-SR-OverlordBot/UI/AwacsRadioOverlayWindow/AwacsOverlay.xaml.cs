using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.AwacsRadioOverlayWindow
{
    /// <summary>
    ///     Interaction logic for RadioOverlayWindow.xaml
    /// </summary>
    public partial class RadioOverlayWindow
    {
        private readonly double _aspectRatio;

        private readonly RadioControlGroup[] _radioControlGroup = new RadioControlGroup[10];

        private readonly DispatcherTimer _updateTimer;

        public static bool AwacsActive; //when false and we're in spectator mode / not in an aircraft the other 7 radios will be disabled

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        private readonly SettingsStore _settings = SettingsStore.Instance;

        public RadioOverlayWindow()
        {
            //load opacity before the intialising as the slider changed
            //method fires after initialisation
            //     var opacity = AppConfiguration.Instance.RadioOpacity;
            AwacsActive = true;

            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = _settings.GetPositionSetting(SettingsKeys.AwacsX).DoubleValue;
            Top = _settings.GetPositionSetting(SettingsKeys.AwacsY).DoubleValue;

            _aspectRatio = MinWidth / MinHeight;

            AllowsTransparency = true;
            //    Opacity = opacity;
            WindowOpacitySlider.Value = Opacity;

            _radioControlGroup[0] = Radio1;
            _radioControlGroup[1] = Radio2;
            _radioControlGroup[2] = Radio3;
            _radioControlGroup[3] = Radio4;
            _radioControlGroup[4] = Radio5;
            _radioControlGroup[5] = Radio6;
            _radioControlGroup[6] = Radio7;
            _radioControlGroup[7] = Radio8;
            _radioControlGroup[8] = Radio9;
            _radioControlGroup[9] = Radio10;


            //allows click and drag anywhere on the window
            ContainerPanel.MouseLeftButtonDown += WrapPanel_MouseLeftButtonDown;

            //      Top = AppConfiguration.Instance.RadioX;
            //        Left = AppConfiguration.Instance.RadioY;

            //     Width = AppConfiguration.Instance.RadioWidth;
            //      Height = AppConfiguration.Instance.RadioHeight;

            //  Window_Loaded(null, null);

            CalculateScale();

            LocationChanged += Location_Changed;

            RadioRefresh(null, null);

            //init radio refresh
            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(80)};
            _updateTimer.Tick += RadioRefresh;
            _updateTimer.Start();
        }

        private static void Location_Changed(object sender, EventArgs e)
        {
            //   AppConfiguration.Instance.RadioX = Top;
            //  AppConfiguration.Instance.RadioY = Left;
        }

        private void RadioRefresh(object sender, EventArgs eventArgs)
        {
            foreach (var radio in _radioControlGroup)
            {
                radio.RepaintRadioReceive();
                radio.RepaintRadioStatus();
            }

            intercom.RepaintRadioStatus();

            var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
            if (dcsPlayerRadioInfo == null) return;
            
            if (_clientStateSingleton.IsConnected && dcsPlayerRadioInfo.IsCurrent())
            {
                ToggleGlobalSimultaneousTransmissionButton.IsEnabled = true;
            }
            else
            {
                ToggleGlobalSimultaneousTransmissionButton.IsEnabled = false;
                ToggleGlobalSimultaneousTransmissionButton.Content = "Simul. Transmission OFF";

                dcsPlayerRadioInfo.simultaneousTransmission = false;

                foreach (var radio in dcsPlayerRadioInfo.radios)
                {
                    radio.simul = false;
                }
            }
        }

        private void WrapPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _settings.GetPositionSetting(SettingsKeys.AwacsX).DoubleValue = Left;
            _settings.GetPositionSetting(SettingsKeys.AwacsY).DoubleValue = Top;

            _settings.Save();


            base.OnClosing(e);

            AwacsActive = false;
            _updateTimer.Stop();
        }

        private void Button_Minimise(object sender, RoutedEventArgs e)
        {
            // Minimising a window without a taskbar icon leads to the window's menu bar still showing up in the bottom of screen
            // Since controls are unusable, but a very small portion of the always-on-top window still showing, we're closing it instead, similar to toggling the overlay
            if (_settings.GetClientSetting(SettingsKeys.RadioOverlayTaskbarHide).BoolValue)
            {
                Close();
            }
            else
            {
                WindowState = WindowState.Minimized;
            }
        }


        private void Button_Close(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void windowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Opacity = e.NewValue;
            //AppConfiguration.Instance.RadioOpacity = Opacity;
        }

        private void containerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //force aspect ratio
            CalculateScale();

            WindowState = WindowState.Normal;
        }

//
//
        private void CalculateScale()
        {
            var yScale = ActualHeight / RadioOverlayWin.MinWidth;
            var xScale = ActualWidth / RadioOverlayWin.MinWidth;
            var value = Math.Max(xScale, yScale);
            ScaleValue = (double) OnCoerceScaleValue(RadioOverlayWin, value);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            if (sizeInfo.WidthChanged)
                Width = sizeInfo.NewSize.Height * _aspectRatio;
            else
                Height = sizeInfo.NewSize.Width / _aspectRatio;

            //  AppConfiguration.Instance.RadioWidth = Width;
            // AppConfiguration.Instance.RadioHeight = Height;
            // Console.WriteLine(this.Height +" width:"+ this.Width);
        }

        #region ScaleValue Depdency Property //StackOverflow: http://stackoverflow.com/questions/3193339/tips-on-developing-resolution-independent-application/5000120#5000120

        public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
            typeof(double), typeof(RadioOverlayWindow),
            new UIPropertyMetadata(1.0, OnScaleValueChanged,
                OnCoerceScaleValue));


        private static object OnCoerceScaleValue(DependencyObject o, object value)
        {
            if (o is RadioOverlayWindow mainWindow)
                return mainWindow.OnCoerceScaleValue((double) value);
            return value;
        }

        private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (o is RadioOverlayWindow mainWindow)
                mainWindow.OnScaleValueChanged((double) e.OldValue, (double) e.NewValue);
        }

        protected virtual double OnCoerceScaleValue(double value)
        {
            if (double.IsNaN(value))
                return 1.0f;

            value = Math.Max(0.1, value);
            return value;
        }

        protected virtual void OnScaleValueChanged(double oldValue, double newValue)
        {
        }

        public double ScaleValue
        {
            get => (double) GetValue(ScaleValueProperty);
            set => SetValue(ScaleValueProperty, value);
        }

        #endregion

        private void ToggleGlobalSimultaneousTransmissionButton_Click(object sender, RoutedEventArgs e)
        {
            var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
            if (dcsPlayerRadioInfo == null) return;
            dcsPlayerRadioInfo.simultaneousTransmission = !dcsPlayerRadioInfo.simultaneousTransmission;

            if (!dcsPlayerRadioInfo.simultaneousTransmission)
            {
                foreach (var radio in dcsPlayerRadioInfo.radios)
                {
                    radio.simul = false;
                }
            }

            ToggleGlobalSimultaneousTransmissionButton.Content = _clientStateSingleton.DcsPlayerRadioInfo.simultaneousTransmission ? "Simul. Transmission ON" : "Simul. Transmission OFF";

            foreach (var radio in _radioControlGroup)
            {
                if (!dcsPlayerRadioInfo.simultaneousTransmission)
                {
                    radio.ToggleSimultaneousTransmissionButton.Content = "Sim. OFF";
                }

                radio.RepaintRadioStatus();
            }
        }
    }
}
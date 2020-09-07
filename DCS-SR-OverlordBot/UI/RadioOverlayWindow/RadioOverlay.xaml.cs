using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow;
using Ciribob.DCS.SimpleRadio.Standalone.Common;

namespace Ciribob.DCS.SimpleRadio.Standalone.Overlay
{
    /// <summary>
    ///     Interaction logic for RadioOverlayWindow.xaml
    /// </summary>
    public partial class RadioOverlayWindow
    {
        private readonly double _aspectRatio;

        private readonly RadioControlGroup[] _radioControlGroup =
            new RadioControlGroup[3];

        private readonly DispatcherTimer _updateTimer;

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        private readonly SettingsStore _settings = SettingsStore.Instance;

        public RadioOverlayWindow()
        {
            //load opacity before the intialising as the slider changed
            //method fires after initialisation
            InitializeComponent();

            WindowStartupLocation = WindowStartupLocation.Manual;

            _aspectRatio = MinWidth / MinHeight;

            AllowsTransparency = true;
            Opacity = _settings.GetPositionSetting(SettingsKeys.RadioOpacity).DoubleValue;
            WindowOpacitySlider.Value = Opacity;

            _radioControlGroup[0] = Radio1;
            _radioControlGroup[1] = Radio2;
            _radioControlGroup[2] = Radio3;

            //allows click and drag anywhere on the window
            ContainerPanel.MouseLeftButtonDown += WrapPanel_MouseLeftButtonDown;

            Left = _settings.GetPositionSetting(SettingsKeys.RadioX).DoubleValue;
            Top = _settings.GetPositionSetting(SettingsKeys.RadioY).DoubleValue;

            Width = _settings.GetPositionSetting(SettingsKeys.RadioWidth).DoubleValue;
            Height = _settings.GetPositionSetting(SettingsKeys.RadioHeight).DoubleValue;

            //  Window_Loaded(null, null);
            CalculateScale();

            RadioRefresh(null, null);

            //init radio refresh
            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(80)};
            _updateTimer.Tick += RadioRefresh;
            _updateTimer.Start();
        }

        private void RadioRefresh(object sender, EventArgs eventArgs)
        {
            foreach (var radio in _radioControlGroup)
            {
                radio.RepaintRadioReceive();
                radio.RepaintRadioStatus();
            }

            Intercom.RepaintRadioStatus();

            var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;
            if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent())
            {
                var availableRadios = dcsPlayerRadioInfo.radios.Count(t => t.modulation != RadioInformation.Modulation.DISABLED);

                if (availableRadios > 1)
                {
                    ControlText.Text = dcsPlayerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.HOTAS ? "HOTAS Controls" : "Cockpit Controls";
                }
                else
                {
                    ControlText.Text = "";
                }
            }
            else
            {
                ControlText.Text = "";
            }

            FocusDcs();
        }

        private long _lastFocus;

        private void FocusDcs()
        {
            if (!_settings.GetClientSetting(SettingsKeys.RefocusDcs).BoolValue) return;
            var overlayWindow = new WindowInteropHelper(this).Handle;

            //focus DCS if needed
            var foreGround = WindowHelper.GetForegroundWindow();

            var localByName = Process.GetProcessesByName("dcs");

            if (localByName.Length <= 0) return;
            //either DCS is in focus OR Overlay window is not in focus
            if (foreGround == localByName[0].MainWindowHandle || overlayWindow != foreGround ||
                IsMouseOver)
            {
                _lastFocus = DateTime.Now.Ticks;
            }
            else if (DateTime.Now.Ticks > _lastFocus + 20000000 && overlayWindow == foreGround)
            {
                WindowHelper.BringProcessToFront(localByName[0]);
            }
        }

        private void WrapPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _settings.GetPositionSetting(SettingsKeys.RadioWidth).DoubleValue = Width;
            _settings.GetPositionSetting(SettingsKeys.RadioHeight).DoubleValue = Height;
            _settings.GetPositionSetting(SettingsKeys.RadioOpacity).DoubleValue = Opacity;
            _settings.GetPositionSetting(SettingsKeys.RadioX).DoubleValue = Left;
            _settings.GetPositionSetting(SettingsKeys.RadioY).DoubleValue = Top;
            _settings.Save();
            base.OnClosing(e);

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
        }

        private void containerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //force aspect ratio
            CalculateScale();

            WindowState = WindowState.Normal;
        }


        private void CalculateScale()
        {
            var yScale = ActualHeight / RadioOverlayWin.MinWidth;
            var xScale = ActualWidth / RadioOverlayWin.MinWidth;
            var value = Math.Min(xScale, yScale);
            ScaleValue = (double) OnCoerceScaleValue(RadioOverlayWin, value);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            if (sizeInfo.WidthChanged)
                Width = sizeInfo.NewSize.Height * _aspectRatio;
            else
                Height = sizeInfo.NewSize.Width / _aspectRatio;


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

        private void RadioOverlayWindow_OnLocationChanged(object sender, EventArgs e)
        {
            //reset last focus so we dont switch back to dcs while dragging
            _lastFocus = DateTime.Now.Ticks;
        }
    }
}
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Discord;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord;
using NLog;
using Npgsql;
using Npgsql.Logging;

namespace DCS_SR_Client
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool loggingReady = false;

        private static readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private static readonly CancellationToken _token = _tokenSource.Token;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);

            var location = AppDomain.CurrentDomain.BaseDirectory;

            //check for opus.dll
            if (!File.Exists(location + "\\opus.dll"))
            {
                MessageBox.Show(
                    $"You are missing the opus.dll - Reinstall using the Installer and don't move the client from the installation directory!",
                    "Installation Error!", MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(1);
            }
            if (!File.Exists(location + "\\speexdsp.dll"))
            {

                MessageBox.Show(
                    $"You are missing the speexdsp.dll - Reinstall using the Installer and don't move the client from the installation directory!",
                    "Installation Error!", MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(1);
            }

            InitNotificationIcon();

            NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite(geographyAsDefault: true);
            NpgsqlLogManager.Provider = new NLogLoggingProvider();
            NpgsqlLogManager.IsParameterLoggingEnabled = true;

            Task.Run(async () => await DiscordClient.Connect());

            // Since this is experimental still and we only want it running on dev bots with discord logging. Gate it behind configuration
            // for the discord guild and channel.
            if (Ciribob.DCS.SimpleRadio.Standalone.Client.Properties.Settings.Default.AtcLogDiscordGuild > 0 &&
                Ciribob.DCS.SimpleRadio.Standalone.Client.Properties.Settings.Default.AtcLogDiscordChannel > 0)
            {
                Task.Run(async () => await Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.Atc.Manager.Instance.Start(_token));
            }
        }

        private void InitNotificationIcon()
        {
            System.Windows.Forms.MenuItem notifyIconContextMenuShow = new System.Windows.Forms.MenuItem
            {
                Index = 0,
                Text = "Show"
            };
            notifyIconContextMenuShow.Click += new EventHandler(NotifyIcon_Show);

            System.Windows.Forms.MenuItem notifyIconContextMenuQuit = new System.Windows.Forms.MenuItem
            {
                Index = 1,
                Text = "Quit"
            };
            notifyIconContextMenuQuit.Click += new EventHandler(NotifyIcon_Quit);

            System.Windows.Forms.ContextMenu notifyIconContextMenu = new System.Windows.Forms.ContextMenu();
            notifyIconContextMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { notifyIconContextMenuShow, notifyIconContextMenuQuit });

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = Ciribob.DCS.SimpleRadio.Standalone.Client.Properties.Resources.audio_headset,
                Visible = true
            };
            _notifyIcon.ContextMenu = notifyIconContextMenu;
            _notifyIcon.DoubleClick += new EventHandler(NotifyIcon_Show);

        }

        private void NotifyIcon_Show(object sender, EventArgs args)
        {
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
        }

        private void NotifyIcon_Quit(object sender, EventArgs args)
        {
            MainWindow.Close();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tokenSource.Cancel();
            Task.Run(() => DiscordClient.Disconnect());
            SRSClientSyncHandler.Instance.ApplicationStopped = true;
            _notifyIcon.Visible = false;
            base.OnExit(e);
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (loggingReady)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error((Exception) e.ExceptionObject, "Received unhandled exception, {0}", e.IsTerminating ? "exiting" : "continuing");
            }
        }
    }
}

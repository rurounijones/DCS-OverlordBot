using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using RurouniJones.DCS.OverlordBot.Discord;
using RurouniJones.DCS.OverlordBot.Network;
using NLog;
using Npgsql;
using Npgsql.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using RurouniJones.DCS.OverlordBot.Util;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Threading.Timer;

namespace RurouniJones.DCS.OverlordBot
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private NotifyIcon _notifyIcon;
        private readonly bool _loggingReady;

        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();

        public App()
        {
            if (!string.IsNullOrEmpty(OverlordBot.Properties.Settings.Default.NewRelicApiKey))
            {
                Sdk.CreateTracerProviderBuilder()
                    .AddSource($"OverlordBot {OverlordBot.Properties.Settings.Default.ServerShortName}")
                    .AddHttpClientInstrumentation()
                    .AddConsoleExporter()
                    .AddNewRelicExporter(config =>
                    {
                        config.ApiKey = OverlordBot.Properties.Settings.Default.NewRelicApiKey;
                        config.ServiceName = $"OverlordBot {OverlordBot.Properties.Settings.Default.ServerShortName}";
                    })
                    .Build();
            }

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

            var location = AppDomain.CurrentDomain.BaseDirectory;

            //check for opus.dll
            if (!File.Exists(location + "\\opus.dll"))
            {
                MessageBox.Show(
                    "You are missing the opus.dll - Reinstall using the Installer and don't move the client from the installation directory!",
                    "Installation Error!", MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(1);
            }

            InitNotificationIcon();

            NpgsqlConnection.GlobalTypeMapper.UseNetTopologySuite(geographyAsDefault: true);
            NpgsqlLogManager.Provider = new NLogLoggingProvider();
            NpgsqlLogManager.IsParameterLoggingEnabled = true;
            _loggingReady = true;

            Task.Run(async () => await DiscordClient.Connect());

            SpeechAuthorizationToken.CancellationToken = TokenSource.Token;
            Task.Run(async () => await SpeechAuthorizationToken.StartTokenRenewTask());

            var _ = new Timer(UpdateAirfields, null, 0, 60000);
        }

        private static void UpdateAirfields(object stateInfo)
        {
            AirfieldUpdater.UpdateAirfields();
        }

        private void InitNotificationIcon()
        {
            var notifyIconContextMenuShow = new MenuItem
            {
                Index = 0,
                Text = "Show"
            };
            notifyIconContextMenuShow.Click += NotifyIcon_Show;

            var notifyIconContextMenuQuit = new MenuItem
            {
                Index = 1,
                Text = "Quit"
            };
            notifyIconContextMenuQuit.Click += NotifyIcon_Quit;

            var notifyIconContextMenu = new ContextMenu();
            notifyIconContextMenu.MenuItems.AddRange(new[] { notifyIconContextMenuShow, notifyIconContextMenuQuit });

            _notifyIcon = new NotifyIcon
            {
                Icon = OverlordBot.Properties.Resources.OverlordBotMinimal,
                Visible = true,
                ContextMenu = notifyIconContextMenu
            };
            _notifyIcon.DoubleClick += NotifyIcon_Show;

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
            TokenSource.Cancel();
            Task.Run(DiscordClient.Disconnect);
            SrsDataClient.ApplicationStopped = true;
            _notifyIcon.Visible = false;
            base.OnExit(e);
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (!_loggingReady) return;
            var logger = LogManager.GetCurrentClassLogger();
            logger.Error((Exception) e.ExceptionObject, "Received unhandled exception, {0}", e.IsTerminating ? "exiting" : "continuing");
        }
    }
}

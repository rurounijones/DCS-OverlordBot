using Discord;
using Discord.WebSocket;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Discord
{
    class DiscordClient
    {
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static DiscordSocketClient _socket;

		private static string _token = Properties.Settings.Default.DiscordToken;
		private static ulong _guildId = Properties.Settings.Default.TransmissionLogDiscordGuild;
		private static ulong _channelId = Properties.Settings.Default.TransmissionLogDiscordChannel;

		public static async Task Connect()
		{
			// This is an optional feature so we will not connect to discord unless the client token is present

			if (_token.Length != 59)
			{
				Logger.Info("Discord token not configured (should be 59 characters), skipping Discord login");
				return;
			}

			Logger.Info("Logging into Discord");

			DiscordSocketConfig config = new DiscordSocketConfig
			{
				LogLevel = LogSeverity.Debug
			};

			_socket = new DiscordSocketClient(config);

			_socket.Log += Log;
			_socket.Disconnected += Reconnect;

			await _socket.LoginAsync(TokenType.Bot, _token);
			await _socket.StartAsync();
			Logger.Info("Logged into Discord");
		}

		public static async Task SendTransmission(string transmission)
		{
			if(_socket == null || _socket.ConnectionState != ConnectionState.Connected)
			{
				return;
			}
			try
			{
				await _socket.GetGuild(_guildId).GetTextChannel(_channelId).SendMessageAsync(transmission);
			} catch(Exception e)
			{
				Logger.Error(e);
			}
		}

		public static async Task Disconnect()
		{
			await _socket.StopAsync();
		}

		private static async Task Reconnect(Exception e)
		{
			Logger.Info("Disconnected from Discord");
			if (e != null)
			{
				Logger.Error(e);
			}
			_socket.Dispose();
			await Connect();
		}

		private static Task Log(LogMessage msg)
		{
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
		}
	}
}
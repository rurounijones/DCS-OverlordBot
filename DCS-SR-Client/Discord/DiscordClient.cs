using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Discord;
using Discord.WebSocket;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Discord
{
    class DiscordClient
    {
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static DiscordSocketClient _socket;

		private static string _token = Properties.Settings.Default.DiscordToken;
		private static ulong _transmissionLogGuildId = Properties.Settings.Default.TransmissionLogDiscordGuild;
		private static ulong _transmissionLogChannelId = Properties.Settings.Default.TransmissionLogDiscordChannel;
		private static ulong _atcLogGuildId = Properties.Settings.Default.AtcLogDiscordGuild;
		private static ulong _atcLogChannelId = Properties.Settings.Default.AtcLogDiscordChannel;

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
			_socket.MessageReceived += ProcessMessage;

			await _socket.LoginAsync(TokenType.Bot, _token);
			await _socket.StartAsync();
			Logger.Info("Logged into Discord");
		}

		private async static Task ProcessMessage(SocketMessage message)
		{
			if (message.Channel.GetType() == typeof(SocketDMChannel)
				&& message.Author.Id == 278154654347427840 // DOLT 1-2 RurouniJones's Discord ID
				&& message.Content.Split(new char[] { ' ' })[0].Equals(Properties.Settings.Default.ServerName))
			{
				string radioId = message.Content.Split(new char[] { ' ' })[1];
				string messageText = string.Join(" ", message.Content.Split(new char[] { ' ' }).Skip(2).ToArray());

				Logger.Info($"Discord message recieved for transmission on radio {radioId}: {messageText}");
				try
				{
					await AudioManager.Instance.BotAudioProviders[int.Parse(radioId)].SendTransmission(messageText);
					await SendTransmission($"Outgoing Transmission:\n{messageText}");
				} catch(KeyNotFoundException ex)
				{
					Logger.Error(ex, $"Could not find radio with key {radioId}");
				}
			}
		}

		public static async Task SendTransmission(string transmission)
		{
			if(_socket == null || _socket.ConnectionState != ConnectionState.Connected)
			{
				return;
			}
			try
			{
				await _socket.GetGuild(_transmissionLogGuildId).GetTextChannel(_transmissionLogChannelId).SendMessageAsync(transmission);
			} catch(Exception e)
			{
				Logger.Error(e);
			}
		}

		public static async Task SendToAtcLogChannel(string navigationMessage)
		{
			if (_socket == null || _socket.ConnectionState != ConnectionState.Connected)
			{
				return;
			}
			try
			{
				await _socket.GetGuild(_atcLogGuildId).GetTextChannel(_atcLogChannelId).SendMessageAsync(navigationMessage);
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		}

		public static async Task Disconnect()
		{
			if (_socket != null)
			{
				await _socket.StopAsync();
			}
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
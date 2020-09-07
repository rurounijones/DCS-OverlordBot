using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Discord;
using Discord.WebSocket;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Discord
{
    internal class DiscordClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static DiscordSocketClient _socket;

        private static readonly string Token = Properties.Settings.Default.DiscordToken;
        private static readonly ulong TransmissionLogDiscordGuildId = Properties.Settings.Default.TransmissionLogDiscordGuild;

        public static async Task Connect()
        {
            // This is an optional feature so we will not connect to discord unless the client token is present

            if (Token.Length != 59)
            {
                Logger.Info("Discord token not configured (should be 59 characters), skipping Discord login");
                return;
            }

            Logger.Info("Logging into Discord");

            var config = new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug
            };

            _socket = new DiscordSocketClient(config);

            _socket.Log += Log;
            _socket.Disconnected += Reconnect;
            _socket.MessageReceived += ProcessMessage;

            try
            {
                await _socket.LoginAsync(TokenType.Bot, Token);
                await _socket.StartAsync();
            }
            catch (Exception ex)
            {
                await Reconnect(ex);
            }
            Logger.Info("Logged into Discord");
        }

        private static async Task ProcessMessage(SocketMessage message)
        {
            if (message.Channel.GetType() == typeof(SocketDMChannel)
                && message.Author.Id == 278154654347427840 // DOLT 1-2 RurouniJones's Discord ID
                && message.Content.Split(' ')[0].Equals(Properties.Settings.Default.ServerName))
            {
                var radioId = message.Content.Split(' ')[1];
                var messageText = string.Join(" ", message.Content.Split(' ').Skip(2).ToArray());

                Logger.Info($"Discord message recieved for transmission on radio {radioId}: {messageText}");
                try
                {
                    var audioProvider = AudioManager.Instance.BotAudioProviders[int.Parse(radioId)];
                    await audioProvider.SendTransmission(messageText);
                    await LogTransmissionToDiscord($"Outgoing Transmission:\n{messageText}", audioProvider.SpeechRecognitionListener.Controller.Radio);
                }
                catch (KeyNotFoundException ex)
                {
                    Logger.Error(ex, $"Could not find radio with key {radioId}");
                } catch(Exception ex)
                {
                    Logger.Error(ex, $"Could not send transmission to radio {radioId}");
                }
            }
        }

        public static async Task LogTransmissionToDiscord(string transmission, RadioInformation radioInfo)
        {
            transmission += $"\nClients on freq {radioInfo.freq / 1000000}MHz: {string.Join(", ", GetClientsOnFrequency(radioInfo))}\n" +
            $"Total / Compatible / On Freq Callsigns : {GetHumanSrsClients().Count} / {GetBotCallsignCompatibleClients().Count} / {GetClientsOnFrequency(radioInfo).Count}\n" +
            $"On Freq percentage of Total / Compatible: { Math.Round(GetClientsOnFrequency(radioInfo).Count / (double)GetHumanSrsClients().Count * 100, 2) }% / " +
            $"{ Math.Round(GetClientsOnFrequency(radioInfo).Count / (double)GetBotCallsignCompatibleClients().Count * 100, 2) }%";

            if (_socket == null || _socket.ConnectionState != ConnectionState.Connected)
            {
                return;
            }
            try
            {
                await _socket.GetGuild(TransmissionLogDiscordGuildId).GetTextChannel(radioInfo.discordTransmissionLogChannelId).SendMessageAsync(transmission);
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

        private static List<string> GetHumanSrsClients()
        {
            var allClients = ConnectedClientsSingleton.Instance.Values;
            return (from client in allClients where client.Name != "OverlordBot" && !client.Name.Contains("ATIS") select client.Name).ToList();
        }

        private static List<string> GetBotCallsignCompatibleClients()
        {
            var allClients = ConnectedClientsSingleton.Instance.Values;
            return (from client in allClients where client.Name != "OverlordBot" && !client.Name.Contains("ATIS") && IsClientNameCompatible(client.Name) select client.Name).ToList();
        }

        private static bool IsClientNameCompatible(string name)
        {
            return Regex.Match(name, @"[a-zA-Z]{3,} \d-\d{1,2}").Success || Regex.Match(name, @"[a-zA-Z]{3,} \d{2,3}").Success;
        }

        private static List<string> GetClientsOnFrequency(RadioInformation radioInfo)
        {
            var clientsOnFreq = ConnectedClientsSingleton.Instance.ClientsOnFreq(radioInfo.freq, RadioInformation.Modulation.AM);
            return (from client in clientsOnFreq where client.Name != "OverlordBot" select client.Name).ToList();
        }
    }
}
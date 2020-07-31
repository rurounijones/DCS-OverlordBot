using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Discord;
using Discord.WebSocket;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Discord
{
    class DiscordClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static DiscordSocketClient _socket;

        private static readonly string _token = Properties.Settings.Default.DiscordToken;
        private static readonly ulong _transmissionLogDiscordGuildId = Properties.Settings.Default.TransmissionLogDiscordGuild;

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

            try
            {
                await _socket.LoginAsync(TokenType.Bot, _token);
                await _socket.StartAsync();
            }
            catch (Exception ex)
            {
                await Reconnect(ex);
            }
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
                    var audioProvider = AudioManager.Instance.BotAudioProviders[int.Parse(radioId)];
                    await audioProvider.SendTransmission(messageText);
                    var radioInfo = audioProvider._speechRecognitionListener.controller.Radio.discordTransmissionLogChannelId;
                    await LogTransmissionToDiscord($"Outgoing Transmission:\n{messageText}", audioProvider._speechRecognitionListener.controller.Radio);
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
            $"Total / Compatible / On Freq Callsigns : {GetHumanSRSClients().Count} / {GetBotCallsignCompatibleClients().Count} / {GetClientsOnFrequency(radioInfo).Count}\n" +
            $"On Freq percentage of Total / Compatible: { Math.Round((double)GetClientsOnFrequency(radioInfo).Count / (double)GetHumanSRSClients().Count * 100, 2) }% / " +
            $"{ Math.Round((double)GetClientsOnFrequency(radioInfo).Count / (double)GetBotCallsignCompatibleClients().Count * 100, 2) }%";

            if (_socket == null || _socket.ConnectionState != ConnectionState.Connected)
            {
                return;
            }
            try
            {
                await _socket.GetGuild(_transmissionLogDiscordGuildId).GetTextChannel(radioInfo.discordTransmissionLogChannelId).SendMessageAsync(transmission);
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

        private static List<string> GetHumanSRSClients()
        {
            var allClients = ConnectedClientsSingleton.Instance.Values;
            List<string> humanClients = new List<string>();
            foreach (var client in allClients)
            {
                if (client.Name != "OverlordBot" && !client.Name.Contains("ATIS"))
                {
                    humanClients.Add(client.Name);
                }
            }
            return humanClients;
        }

        private static List<string> GetBotCallsignCompatibleClients()
        {
            var allClients = ConnectedClientsSingleton.Instance.Values;
            List<string> compatibleClients = new List<string>();
            foreach (var client in allClients)
            {
                if (client.Name != "OverlordBot" && !client.Name.Contains("ATIS") && IsClientNameCompatible(client.Name))
                {
                    compatibleClients.Add(client.Name);
                }
            }
            return compatibleClients;
        }

        private static bool IsClientNameCompatible(string name)
        {
            return Regex.Match(name, @"[a-zA-Z]{3,} \d-\d{1,2}").Success || Regex.Match(name, @"[a-zA-Z]{3,} \d{2,3}").Success;
        }

        private static List<string> GetClientsOnFrequency(RadioInformation radioInfo)
        {
            var clientsOnFreq = ConnectedClientsSingleton.Instance.ClientsOnFreq(radioInfo.freq, RadioInformation.Modulation.AM);
            List<string> clients = new List<string>();
            foreach (var client in clientsOnFreq)
            {
                if (client.Name != "OverlordBot")
                {
                    clients.Add(client.Name);
                }
            }
            return clients;
        }
    }
}
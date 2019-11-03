using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Network;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Setting;
using Ciribob.DCS.SimpleRadio.Standalone.Server.Settings;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Server.Network
{
    internal class UDPVoiceRouter
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<string, SRClient> _clientsList;
        private readonly IEventAggregator _eventAggregator;

        private readonly BlockingCollection<OutgoingVoice> _outGoing = new BlockingCollection<OutgoingVoice>();
        private readonly CancellationTokenSource _outgoingCancellationToken = new CancellationTokenSource();

        private readonly CancellationTokenSource _pendingProcessingCancellationToken = new CancellationTokenSource();

        private readonly BlockingCollection<PendingPacket> _pendingProcessingPackets =
            new BlockingCollection<PendingPacket>();

        private readonly ServerSettingsStore _serverSettings = ServerSettingsStore.Instance;
        private UdpClient _listener;

        private volatile bool _stop;

        private static readonly List<int> _emptyBlockedRadios = new List<int>(); // Used in radio reachability check below, server does not track blocked radios, so forward all

        public UDPVoiceRouter(ConcurrentDictionary<string, SRClient> clientsList, IEventAggregator eventAggregator)
        {
            _clientsList = clientsList;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
        }

        public void Listen()
        {
            //start threads
            //packets that need processing
            new Thread(ProcessPackets).Start();
            //outgoing packets
            new Thread(SendPendingPackets).Start();

            var port = _serverSettings.GetServerPort();
            _listener = new UdpClient();
            _listener.AllowNatTraversal(true);
            _listener.ExclusiveAddressUse = true;
            _listener.DontFragment = true;
            _listener.Client.DontFragment = true;
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            while (!_stop)
                try
                {
                    var groupEP = new IPEndPoint(IPAddress.Any, port);
                    var rawBytes = _listener.Receive(ref groupEP);

                    if (rawBytes?.Length == 22)
                    {
                        try
                        {
                            //lookup guid here
                            //22 bytes are guid!
                            var guid = Encoding.ASCII.GetString(
                                rawBytes, 0, 22);

                            if (_clientsList.ContainsKey(guid))
                            {
                                var client = _clientsList[guid];
                                client.VoipPort = groupEP;

                                //send back ping UDP
                                _listener.Send(rawBytes, rawBytes.Length, groupEP);
                            }
                        }
                        catch (Exception ex)
                        {
                            //dont log because it slows down thread too much...
                        }
                    }
                    else if (rawBytes?.Length > 22)
                        _pendingProcessingPackets.Add(new PendingPacket
                        {
                            RawBytes = rawBytes,
                            ReceivedFrom = groupEP
                        });

                }
                catch (Exception e)
                {
                      Logger.Error(e,"Error receving audio UDP for client " + e.Message);
                }

            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }
        }

        public void RequestStop()
        {
            _stop = true;
            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }

            _outgoingCancellationToken.Cancel();
            _pendingProcessingCancellationToken.Cancel();
        }

        private void ProcessPackets()
        {
            while (!_stop)
                try
                {
                    PendingPacket udpPacket = null;
                    _pendingProcessingPackets.TryTake(out udpPacket, 100000, _pendingProcessingCancellationToken.Token);

                    if (udpPacket != null)
                    {
                        //last 22 bytes are guid!
                        var guid = Encoding.ASCII.GetString(
                            udpPacket.RawBytes, udpPacket.RawBytes.Length - 22, 22);

                        if (_clientsList.ContainsKey(guid))
                        {
                            var client = _clientsList[guid];
                            client.VoipPort = udpPacket.ReceivedFrom;

                            var spectatorAudioDisabled =
                                _serverSettings.GetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue;

                            if ((client.Coalition == 0 && spectatorAudioDisabled) || client.Muted)
                            {
                                // IGNORE THE AUDIO
                            }
                            else
                            {
                                try
                                {
                                    //decode
                                    var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(udpPacket.RawBytes);

                                    if ((udpVoicePacket != null) && (udpVoicePacket.Modulations[0] != 4))
                                        //magical ping ignore message 4 - its an empty voip packet to intialise VoIP if
                                        //someone doesnt transmit
                                    {
                                        var outgoingVoice = GenerateOutgoingPacket(udpVoicePacket, udpPacket, client);

                                        if (outgoingVoice != null)
                                        {
                                            //Add to the processing queue
                                            _outGoing.Add(outgoingVoice);

                                            //mark as transmitting for the UI
                                            double mainFrequency = udpVoicePacket.Frequencies.FirstOrDefault();
                                            // Only trigger transmitting frequency update for "proper" packets (excluding invalid frequencies and magic ping packets with modulation 4)
                                            if (mainFrequency > 0 && udpVoicePacket.Modulations[0] != 4)
                                            {
                                                RadioInformation.Modulation mainModulation = (RadioInformation.Modulation)udpVoicePacket.Modulations[0];
                                                if (mainModulation == RadioInformation.Modulation.INTERCOM)
                                                {
                                                    client.TransmittingFrequency = "INTERCOM";
                                                }
                                                else
                                                {
                                                    client.TransmittingFrequency = $"{(mainFrequency / 1000000).ToString("0.000", CultureInfo.InvariantCulture)} {mainModulation}";
                                                }
                                                client.LastTransmissionReceived = DateTime.Now;
                                            }
                                        }

                                    }
                                }
                                catch (Exception)
                                {
                                    //Hide for now, slows down loop to much....
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("Failed to Process UDP Packet: " + ex.Message);
                }
        }

        private
            void SendPendingPackets()
        {
            //_listener.Send(bytes, bytes.Length, ip);
            while (!_stop)
                try
                {
                    OutgoingVoice udpPacket = null;
                    _outGoing.TryTake(out udpPacket, 100000, _pendingProcessingCancellationToken.Token);

                    if (udpPacket != null)
                    {
                        var bytes = udpPacket.ReceivedPacket;
                        var bytesLength = bytes.Length;
                        foreach (var outgoingEndPoint in udpPacket.OutgoingEndPoints)
                            try
                            {
                                _listener.Send(bytes, bytesLength, outgoingEndPoint);
                            }
                            catch (Exception ex)
                            {
                                //dont log, slows down too much...
                            }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("Error processing Sending Queue UDP Packet: " + ex.Message);
                }
        }

        private OutgoingVoice GenerateOutgoingPacket(UDPVoicePacket udpVoice, PendingPacket pendingPacket,
            SRClient fromClient)
        {
            var outgoingList = new HashSet<IPEndPoint>();

            var coalitionSecurity =
                _serverSettings.GetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue;

            var guid = fromClient.ClientGuid;

            foreach (var client in _clientsList)
            {
                if (!client.Key.Equals(guid))
                {
                    var ip = client.Value.VoipPort;

                    // check that either coalition radio security is disabled OR the coalitions match
                    if ((ip != null) && (!coalitionSecurity || (client.Value.Coalition == fromClient.Coalition)))
                    {

                        var radioInfo = client.Value.RadioInfo;

                        if (radioInfo != null)
                        {
                            for (int i = 0; i < udpVoice.Frequencies.Length; i++)
                            {

                                RadioReceivingState radioReceivingState = null;
                                bool decryptable;
                                var receivingRadio = radioInfo.CanHearTransmission(udpVoice.Frequencies[i],
                                    (RadioInformation.Modulation)udpVoice.Modulations[i],
                                    udpVoice.Encryptions[i],
                                    udpVoice.UnitId,
                                    _emptyBlockedRadios,
                                    out radioReceivingState,
                                    out decryptable);

                                //only send if we can hear!
                                if (receivingRadio != null)
                                {
                                    outgoingList.Add(ip);
                                }
                            }


                        }
                    }
                }
                else
                {
                    var ip = client.Value.VoipPort;

                    if (ip != null)
                    {
                        //DEBUG send back to sending client
                         //outgoingList.Add(ip);
                    }
                }
            }

            if (outgoingList.Count > 0)
            {
                return new OutgoingVoice
                {
                    OutgoingEndPoints = outgoingList.ToList(),
                    ReceivedPacket = pendingPacket.RawBytes
                };
            }
            else
            {
                return null;
            }
        }
    }
}
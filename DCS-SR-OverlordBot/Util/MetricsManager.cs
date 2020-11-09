using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Ciribob.DCS.SimpleRadio.Standalone.Common;
using NewRelic.Telemetry;
using NewRelic.Telemetry.Metrics;
using RurouniJones.DCS.OverlordBot.Audio.Managers;
using RurouniJones.DCS.OverlordBot.Controllers;
using RurouniJones.DCS.OverlordBot.UI;

namespace RurouniJones.DCS.OverlordBot.Util
{
    class MetricsManager
    {
        private readonly MetricDataSender _dataSender;
        private readonly Timer _checkTimer;

        private static readonly string ServiceName = $"OverlordBot {Properties.Settings.Default.ServerShortName}";
        private static readonly Guid ServiceInstanceId = Guid.NewGuid();
        
        private static MetricsManager _instance;
        private static readonly object Lock = new object();

        private static readonly ConcurrentQueue<KeyValuePair<string, string>> RadioCallIntents = new ConcurrentQueue<KeyValuePair<string, string>>();
        
        public static MetricsManager Instance
        {
            get
            {
                lock (Lock)
                {
                    if (_instance == null)
                    {
                        _instance = new MetricsManager();
                    }
                }
                return _instance;
            }
        }

        public void RecordRadioCall(string intent, string frequency)
        {
            RadioCallIntents.Enqueue(new KeyValuePair<string, string>(intent, frequency));
        }

        private MetricsManager()
        {
            var telemetryConfig = new TelemetryConfiguration
            {
                ApiKey = Properties.Settings.Default.NewRelicApiKey,
                ServiceName = ServiceName
            };
            _dataSender = new MetricDataSender(telemetryConfig);

            _checkTimer = new Timer(30000);
            _checkTimer.Elapsed += (s, e) => CollectAndPostMetrics();
        }

        private void CollectAndPostMetrics()
        {
            var attributes = new Dictionary<string, object>
            {
                {"service.instance.id", ServiceInstanceId.ToString()},
                {"service.name",  ServiceName}
            };

            // First get overall metrics
            var client = MainWindow.AudioManagers.First().Client;

            var playersOnSrs = NewRelicMetric.CreateGaugeMetric("PlayersOnSrs",
                null, 
                new Dictionary<string, object> { {"callsign", "HasCallsign"} }, 
                client.GetBotCallsignCompatibleClients().Count);
 
            var players = client.GetHumanSrsClientNames();
            foreach (var sl2 in  client.GetBotCallsignCompatibleClients() )
            {
                players.Remove(sl2);
            }
            var botCompatiblePlayersOnSrs = NewRelicMetric.CreateGaugeMetric("PlayersOnSrs",
                null,
                new Dictionary<string, object> { {"callsign", "NoCallsign"} },
                players.Count);

            // Get Thread metrics
            var warningCheckers =  NewRelicMetric.CreateGaugeMetric("CheckerThreads",
                null,
                new Dictionary<string, object> { {"type", "WarningRadius"} },
                WarningRadiusChecker.WarningChecks.Count);

            var taxiCheckers =  NewRelicMetric.CreateGaugeMetric("CheckerThreads",
                null,
                new Dictionary<string, object> { {"type", "Taxi"} },
                TaxiProgressChecker.TaxiChecks.Count);
            
            var approachCheckers =  NewRelicMetric.CreateGaugeMetric("CheckerThreads",
                null,
                new Dictionary<string, object> { {"type", "Approach"} },
                ApproachChecker.ApproachChecks.Count);

            var metrics = new List<NewRelicMetric> { playersOnSrs, botCompatiblePlayersOnSrs, warningCheckers, taxiCheckers, approachCheckers };

            var frequencyCount = new Dictionary<double, int>();

            List<double> botFreqs = new List<double>();

            // Get per-client metrics

            // Get stats for the frequencies the bot is listening on
            foreach (var audioManager in MainWindow.AudioManagers)
            {
                var radioInfo = audioManager.PlayerRadioInfo.radios.First();
                botFreqs.Add(radioInfo.freq);
                metrics.Add(NewRelicMetric.CreateGaugeMetric("PlayersOnFrequency", 
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        new Dictionary<string, object> { {"frequency", $"{radioInfo.freq / 1000000} - Bot {radioInfo.botType}"} }, 
                        audioManager.Client.GetHumansOnFreq(audioManager.PlayerRadioInfo.radios.First()).Count));
                metrics.Add(NewRelicMetric.CreateGaugeMetric("PlayersOnBotFrequency", 
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new Dictionary<string, object> { {"frequency", $"{radioInfo.freq / 1000000} - Bot {radioInfo.botType}"} }, 
                    audioManager.Client.GetHumansOnFreq(audioManager.PlayerRadioInfo.radios.First()).Count));
            }


            // Get stats for the frequencies the players are on that the bot may not be on
            foreach (var playerClient in MainWindow.AudioManagers.First().Client.GetHumanSrsClients())
            {
                // We just want people in aircraft
                if (playerClient.Coalition == 0 || playerClient.RadioInfo.unit.Equals("External AWACS") ||  playerClient.RadioInfo.unit.Equals("CA"))
                    continue;

                foreach (var radio in playerClient.RadioInfo.radios)
                {
                    if (botFreqs.Contains(radio.freq) || (int) radio.freq == 1 || radio.modulation == RadioInformation.Modulation.INTERCOM)
                        continue;
                    var freq = radio.freq;
                    if (frequencyCount.ContainsKey(freq))
                    {
                        frequencyCount[freq] += 1;
                    }
                    else
                    {
                        frequencyCount.Add(freq, 1);
                    }
                }
            }

            foreach (var data in frequencyCount)
            {
                metrics.Add(NewRelicMetric.CreateGaugeMetric("PlayersOnFrequency",
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    new Dictionary<string, object> {{"frequency", $"{data.Key / 1000000}"}},
                    data.Value));
            }

            // Radio Call counts
            var counts = new Dictionary<string, Dictionary<string, int>>();
            
            while(RadioCallIntents.Count > 0)
            {
                RadioCallIntents.TryDequeue(out var call);
                if(!counts.ContainsKey(call.Key))
                    counts.Add(call.Key, new Dictionary<string, int> { {call.Value, 1} });
                else if(!counts[call.Key].ContainsKey(call.Value))
                    counts[call.Key].Add(call.Value, 1);
                else
                  counts[call.Key][call.Value] += 1;
            }

            foreach (var count in counts)
            {
                var intent = count.Key;

                foreach (var frequencyCounts in count.Value)
                {
                    var frequency = frequencyCounts.Key;
                    var callCount = frequencyCounts.Value;

                    metrics.Add(NewRelicMetric.CreateGaugeMetric("RadioCalls",
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        new Dictionary<string, object>
                        {
                            {"intent", intent},
                            {"frequency", frequency}
                        },
                        callCount));
                }
            }


            var batchProperties = new NewRelicMetricBatchCommonProperties(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), null, attributes);
            var batch = new NewRelicMetricBatch(metrics, batchProperties);

            _dataSender.SendDataAsync(batch);
        }

        public void Start()
        {
            _checkTimer.Start();
        }

        public void Stop()
        {
            _checkTimer.Stop();
        }
    }
}

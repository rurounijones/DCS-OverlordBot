using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NLog;

namespace RurouniJones.DCS.OverlordBot.SpeechOutput
{
    internal class Speaker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly SpeechConfig SpeechConfig = SpeechConfig.FromSubscription(Properties.Settings.Default.SpeechSubscriptionKey, Properties.Settings.Default.SpeechRegion);

        private static readonly RadioStreamWriter StreamWriter = new RadioStreamWriter(null);
        private static readonly AudioConfig AudioConfig = AudioConfig.FromStreamOutput(StreamWriter);

        public static async Task<byte[]> CreateResponse(string text)
        {
            using (Constants.ActivitySource.StartActivity("Speaker.CreateResponse"))
            {
                using (var semaphore = new Semaphore(1, 1, "SpeechOutputSemaphore"))
                {
                    SpeechSynthesizer synthesizer = null;
                    try
                    {
                        using (Constants.ActivitySource.StartActivity("WaitingOnSemaphore"))
                        {
                            semaphore.WaitOne();
                        }

                        using (Constants.ActivitySource.StartActivity("InitializingSpeechSynthesizer"))
                        {
                            synthesizer = new SpeechSynthesizer(SpeechConfig, AudioConfig);
                        }

                        SpeechSynthesisResult speechSynthesisResult;

                        using (Constants.ActivitySource.StartActivity("SpeakSsmlAsync"))
                        {
                            speechSynthesisResult = await synthesizer.SpeakSsmlAsync(text);
                        }

                        using (Constants.ActivitySource.StartActivity("ProcessSpeechSynthesisResult"))
                        {
                            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                            switch (speechSynthesisResult.Reason)
                            {
                                case ResultReason.SynthesizingAudioCompleted:
                                    Logger.Debug($"Speech synthesized to speaker for text [{text}]");
                                    Logger.Debug($"Audio size: {speechSynthesisResult.AudioData.Length}");
                                    return speechSynthesisResult.AudioData;
                                case ResultReason.Canceled:
                                {
                                    var cancellation =
                                        SpeechSynthesisCancellationDetails.FromResult(speechSynthesisResult);
                                    Logger.Error("Speech Synthesis cancelled");
                                    Logger.Error($"Reason: {cancellation.Reason}");
                                    Logger.Error($"ErrorCode: {cancellation.ErrorCode}");
                                    Logger.Error($"ErrorDetails: [{cancellation.ErrorDetails}]");
                                    return null;
                                }
                                default:
                                    Logger.Error(
                                        $"Unexpected Speech Synthesis Result {speechSynthesisResult.Reason}");
                                    return null;
                            }
                        }
                    }
                    finally
                    {
                        synthesizer?.Dispose();
                        semaphore.Release();
                    }
                }
            }
        }
    }
}

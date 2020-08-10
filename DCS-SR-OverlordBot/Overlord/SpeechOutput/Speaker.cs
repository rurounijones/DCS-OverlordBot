using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NLog;
using System.Threading;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput
{
    class Speaker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static SpeechConfig _speechConfig = SpeechConfig.FromSubscription(Properties.Settings.Default.SpeechSubscriptionKey, Properties.Settings.Default.SpeechRegion);

        private static RadioStreamWriter _streamWriter = new RadioStreamWriter(null);
        private static AudioConfig _audioConfig = AudioConfig.FromStreamOutput(_streamWriter);

        public static async Task<byte[]> CreateResponse(string text)
        {
            using (Semaphore semaphore = new Semaphore(1, 1, "SpeechOutputSemaphore"))
            {
                try
                {
                    semaphore.WaitOne();
                    using (var synthesizer = new SpeechSynthesizer(_speechConfig, _audioConfig))
                    {
                        using (var textresult = await synthesizer.SpeakSsmlAsync(text))
                        {
                            if (textresult.Reason == ResultReason.SynthesizingAudioCompleted)
                            {
                                Logger.Debug($"Speech synthesized to speaker for text [{text}]");
                                Logger.Debug($"Audio size: {textresult.AudioData.Length}");
                                return textresult.AudioData;
                            }
                            else if (textresult.Reason == ResultReason.Canceled)
                            {
                                var cancellation = SpeechSynthesisCancellationDetails.FromResult(textresult);
                                Logger.Debug($"CANCELED: Reason={cancellation.Reason}");

                                if (cancellation.Reason == CancellationReason.Error)
                                {
                                    Logger.Debug($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                                    Logger.Debug($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                                    Logger.Debug($"CANCELED: Did you update the subscription info?");
                                }
                            }
                            return null;
                        }
                    }
                }
                finally { semaphore.Release(); }
            }
        }
    }
}

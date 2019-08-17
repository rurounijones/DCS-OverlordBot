using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Audio.Managers;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord.SpeechOutput
{
    class Speaker
    {
        private static SpeechConfig _speechConfig = SpeechConfig.FromSubscription("YourSubscription", "YourRegion");

        private static RadioStreamWriter _streamWriter = new RadioStreamWriter(null);
        private static AudioConfig _audioConfig = AudioConfig.FromStreamOutput(_streamWriter);
        
        public static async Task<byte[]> CreateResponse(string text)
        {
            using (var synthesizer = new SpeechSynthesizer(_speechConfig, _audioConfig))
            {
                using (var textresult = await synthesizer.SpeakTextAsync(text))
                {
                    if (textresult.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        Console.WriteLine($"Speech synthesized to speaker for text [{text}]");
                        return textresult.AudioData;
                    }
                    else if (textresult.Reason == ResultReason.Canceled)
                    {
                        var cancellation = SpeechSynthesisCancellationDetails.FromResult(textresult);
                        Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                        if (cancellation.Reason == CancellationReason.Error)
                        {
                            Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                            Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                            Console.WriteLine($"CANCELED: Did you update the subscription info?");
                        }
                    }
                    return null;
                }
            }
        }
    }
}

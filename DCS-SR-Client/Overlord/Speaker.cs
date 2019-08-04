using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    class Speaker
    {
        SpeechSynthesizer _synthesizer;
        AudioConfig _streamConfig; // If this gets GC'd then we lose the RadioStreamWriter.

        public Speaker(BufferedWaveProvider provider)
        {
            var streamWriter = new RadioStreamWriter(provider);
            _streamConfig = AudioConfig.FromStreamOutput(streamWriter);
            var speechConfig = SpeechConfig.FromSubscription("FILL_ME_IN", "FILL_ME_IN");

            _synthesizer = new SpeechSynthesizer(speechConfig, _streamConfig);
        }

        public async Task SendResponse(string text)
        {
            Console.WriteLine($"Speech synthesized to speaker for text [{text}]");
            using (var textresult = await _synthesizer.SpeakTextAsync(text))
            {
                if (textresult.Reason == ResultReason.SynthesizingAudioCompleted)
                {
                    // No-op
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
            }
        }
    }
}

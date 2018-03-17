using Microsoft.CognitiveServices.SpeechRecognition;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CustomSpeechServiceSample
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();

            builder.SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                   .AddEnvironmentVariables();

            IConfigurationRoot configuration = builder.Build();

            var options = configuration.Get<CustomSpeechServiceOptions>();

            Task.Run(() => RunAsync(options)).GetAwaiter().GetResult();
        }

        private static async Task RunAsync(CustomSpeechServiceOptions options)
        {
            for (int index = 0; index < options.Endpoints.Length; index++)
            {
                await SendAudioAsync(options, index);
            }
        }

        private static Task<RecognitionResult> SendAudioAsync(CustomSpeechServiceOptions options, int endpointIndex)
        {
            var tcs = new TaskCompletionSource<RecognitionResult>();

            using (var client = SpeechRecognitionServiceFactory.CreateDataClient(SpeechRecognitionMode.ShortPhrase,
                                                                                 "en-us",
                                                                                 options.PrimaryKey,
                                                                                 options.SecondaryKey,
                                                                                 options.Endpoints[endpointIndex]))
            {
                client.AuthenticationUri = "https://westus.api.cognitive.microsoft.com/sts/v1.0/issueToken";

                client.OnResponseReceived += (sender, e) =>
                {
                    tcs.SetResult(e.PhraseResponse);
                };

                client.OnConversationError += (sender, e) =>
                {
                    tcs.SetException(new InvalidOperationException(e.SpeechErrorText));
                };

                using (var audioFileStream = new FileStream(Path.Combine(Directory.GetCurrentDirectory(), options.AudioFile), FileMode.Open, FileAccess.Read))
                {
                    int bytesRead = 0;
                    byte[] buffer = new byte[1024];

                    try
                    {
                        do
                        {
                            bytesRead = audioFileStream.Read(buffer, 0, buffer.Length);
                            client.SendAudio(buffer, bytesRead);
                        }
                        while (bytesRead > 0);
                    }
                    finally
                    {
                        client.EndAudio();
                    }
                }

                return tcs.Task;
            }
        }
    }
}

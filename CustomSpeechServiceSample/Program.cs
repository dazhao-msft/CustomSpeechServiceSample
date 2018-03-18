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
                   .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            var options = configuration.Get<CustomSpeechServiceOptions>();

            Task.Run(() => RunAsync(options)).GetAwaiter().GetResult();

            Console.Read();
        }

        private static async Task RunAsync(CustomSpeechServiceOptions options)
        {
            for (int index = 0; index < options.ServiceUrls.Length; index++)
            {
                Console.WriteLine($"Service URL: {options.ServiceUrls[index]}");

                var response = await SendRequestAsync(options, index);

                Console.WriteLine($"RecognitionStatus: {response.RecognitionStatus}");

                foreach (var result in response.Results)
                {
                    Console.WriteLine($"Result DisplayText: {result.DisplayText}");
                    Console.WriteLine($"Result Confidence: {result.Confidence}");
                }
            }
        }

        private static async Task<RecognitionResult> SendRequestAsync(CustomSpeechServiceOptions options, int serviceUrlIndex)
        {
            using (var client = SpeechRecognitionServiceFactory.CreateDataClient(SpeechRecognitionMode.ShortPhrase,
                                                                                 "en-us",
                                                                                 options.PrimaryKey,
                                                                                 options.SecondaryKey,
                                                                                 options.ServiceUrls[serviceUrlIndex]))
            {
                var tcs = new TaskCompletionSource<RecognitionResult>();

                client.AuthenticationUri = options.AuthenticationUrl;

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

                return await tcs.Task;
            }
        }
    }
}

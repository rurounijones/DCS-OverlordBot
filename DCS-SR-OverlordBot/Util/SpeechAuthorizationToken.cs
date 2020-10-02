using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using OpenTelemetry.Trace;

namespace RurouniJones.DCS.OverlordBot.Util
{
    class SpeechAuthorizationToken
    {
        public static volatile string AuthorizationToken;
        public static CancellationToken CancellationToken { private get; set; }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // Authorization token expires every 10 minutes. Renew it every 9 minutes.
        private static readonly TimeSpan RefreshTokenDuration = TimeSpan.FromMinutes(9);

        // If the fetching of the token failed then try more quickly.
        private static readonly TimeSpan ErrorRefreshTokenDuration = TimeSpan.FromSeconds(10);


        private static async Task<string> GetToken()
        {
            using (var activity = Constants.ActivitySource.StartActivity("SpeechAuthorizationToken.GetToken"))
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
                        Properties.Settings.Default.SpeechSubscriptionKey);
                    var uriBuilder = new UriBuilder("https://" + Properties.Settings.Default.SpeechRegion +
                                                    ".api.cognitive.microsoft.com/sts/v1.0/issueToken");

                    using (var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null))
                    {
                        if (result.IsSuccessStatusCode)
                        {
                            return await result.Content.ReadAsStringAsync();
                        }

                        var exception = new HttpRequestException(
                            $"Cannot get token from {uriBuilder}. Error: {result.StatusCode}");
                        activity?.RecordException(exception);
                        Logger.Error(exception, $"Could not retrieve Authorization Token");
                        
                        return null;
                    }
                }
            }
        }

        public static Task StartTokenRenewTask()
        {
            Logger.Debug("Starting Token Renew Task");

            return Task.Run(async () =>
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    if (!CancellationToken.IsCancellationRequested)
                    {
                        AuthorizationToken = await GetToken();
                    }

                    if (AuthorizationToken == null)
                    {
                        // Errored, try again more quickly
                        await Task.Delay(ErrorRefreshTokenDuration, CancellationToken);
                    }
                    else
                    {
                        await Task.Delay(RefreshTokenDuration, CancellationToken);
                    }
                }
            }, CancellationToken);
        }
    }
}

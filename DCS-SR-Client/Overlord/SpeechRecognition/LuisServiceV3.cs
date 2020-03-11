using NLog;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Overlord
{
    class LuisServiceV3
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Guid _luisAppId = Guid.Parse(Properties.Settings.Default.ShadowLuisAppId);
        private static readonly string _luisApiKey = Properties.Settings.Default.ShadowLuisEndpointKey;

        public static async Task RecognizeAsync(string inputText)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(inputText);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _luisApiKey);

            // Request parameters
            queryString["verbose"] = "true";
            queryString["log"] = "false";
            queryString["show-all-intents"] = "false";
            var uri = $"https://westus.api.cognitive.microsoft.com/luis/v3.0-preview/apps/{_luisAppId}/slots/production/predict?query={inputText}&{queryString}";

            Logger.Info("SHADOW LUIS RESPONSE: " + await client.GetAsync(new Uri(uri, UriKind.Absolute)).Result.Content.ReadAsStringAsync());

            client.Dispose();
        }
    }
}

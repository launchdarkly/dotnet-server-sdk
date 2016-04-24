using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    public class FeatureRequestor
    {
        private static ILog Logger = LogProvider.For<FeatureRequestor>();
        private Configuration _configuration;
        private readonly HttpClient _httpClient;

        public FeatureRequestor(Configuration config)
        {
            _httpClient = config.HttpClient;
            _configuration = config;
        }

        public async Task<IDictionary<string, Feature>> MakeAllRequest(bool latest)
        {
            string resource = latest ? "/api/eval/latest-features" : "/api/eval/features";
           // do this instead: https://msdn.microsoft.com/en-us/library/system.net.cache.requestcachepolicy(v=vs.110).aspx
            using (var response = await _httpClient.GetAsync(resource).ConfigureAwait(false))
            {
                handleResponseStatus(response.StatusCode, null);
                return await response.Content.ReadAsAsync<IDictionary<string, Feature>>().ConfigureAwait(false);
            }
        }

        private void handleResponseStatus(HttpStatusCode status, string featureKey)
        {
            if (status != HttpStatusCode.OK)
            {
                if (status == HttpStatusCode.Unauthorized)
                {
                    Logger.Error("Invalid API key");
                }
                else if (status == HttpStatusCode.NotFound)
                {
                    if (featureKey != null)
                    {
                        Logger.Error("Unknown feature key: " + featureKey);
                    }
                    else
                    {
                        Logger.Error("Resource not found");
                    }
                }
                else
                {
                    Logger.Error("Unexpected status code: " + status);
                }
                //TODO: probably not exactly this:
                throw new Exception("Failed to fetch flag(s) with status code: " + status);
            }
        }
    }

}

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

        public IDictionary<string, FeatureFlag> MakeAllRequest(bool latest)
        {
            string resource = latest ? "api/eval/latest-features" : "api/eval/features";
            var uri = new Uri(_configuration.BaseUri.AbsoluteUri + resource);
            Logger.Debug("Getting all features with uri: " + uri.AbsoluteUri);
            using (var responseTask = _httpClient.GetAsync(uri))
            {
                responseTask.ConfigureAwait(false);
                var response = responseTask.Result;
                handleResponseStatus(response.StatusCode);
                var contentTask = response.Content.ReadAsAsync<IDictionary<string, FeatureFlag>>();
                contentTask.ConfigureAwait(false);
                return contentTask.Result;
            }
        }

        private void handleResponseStatus(HttpStatusCode status)
        {
            if (status != HttpStatusCode.OK)
            {
                if (status == HttpStatusCode.Unauthorized)
                {
                    Logger.Error("Invalid API key");
                }
                else if (status == HttpStatusCode.NotFound)
                {
                    Logger.Error("Resource not found");
                }
                else
                {
                    Logger.Error("Unexpected status code: " + status);
                }
                throw new Exception("Failed to fetch feature flags with status code: " + status);
            }
        }
    }

}

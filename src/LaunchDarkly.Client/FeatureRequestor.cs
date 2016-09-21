using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    class FeatureRequestor
    {
        private static ILogger Logger = LdLogger.CreateLogger<FeatureRequestor>();
        private Configuration _configuration;
        private readonly HttpClient _httpClient;

        internal FeatureRequestor(Configuration config)
        {
            _httpClient = config.HttpClient;
            _configuration = config;
        }

        internal IDictionary<string, FeatureFlag> MakeAllRequest(bool latest)
        {
            string resource = latest ? "sdk/latest-flags" : "sdk/flags";
            var uri = new Uri(_configuration.BaseUri.AbsoluteUri + resource);
            Logger.LogDebug("Getting all features with uri: " + uri.AbsoluteUri);
            using (var responseTask = _httpClient.GetAsync(uri))
            {
                responseTask.ConfigureAwait(false);
                var response = responseTask.Result;
                handleResponseStatus(response.StatusCode);
                var contentTask = response.Content.ReadAsStringAsync();
                contentTask.ConfigureAwait(false);
                return JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(contentTask.Result);
            }
        }

        private void handleResponseStatus(HttpStatusCode status)
        {
            if (status != HttpStatusCode.OK)
            {
                if (status == HttpStatusCode.Unauthorized)
                {
                    Logger.LogError("Invalid SDK key");
                }
                else if (status == HttpStatusCode.NotFound)
                {
                    Logger.LogError("Resource not found");
                }
                else
                {
                    Logger.LogError("Unexpected status code: " + status);
                }
                throw new Exception("Failed to fetch feature flags with status code: " + status);
            }
        }
    }

}

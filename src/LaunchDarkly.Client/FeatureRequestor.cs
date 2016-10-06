using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    class FeatureRequestor
    {
        private static ILog Logger = LogProvider.For<FeatureRequestor>();
        private Configuration _configuration;
        private readonly HttpClient _httpClient;

        internal FeatureRequestor(Configuration config)
        {
            _httpClient = config.HttpClient;
            _configuration = config;
        }

        internal async Task<IDictionary<string, FeatureFlag>> MakeAllRequestAsync()
        {
            var uri = new Uri(_configuration.BaseUri.AbsoluteUri + "sdk/latest-flags");
            Logger.Debug("Getting all features with uri: " + uri.AbsoluteUri);

            using (var responseTask = _httpClient.GetAsync(uri))
            {
                var response = await responseTask.ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(content);
            }
        }
    }

}

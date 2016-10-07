using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    class FeatureRequestor
    {
        private static ILog Logger = LogProvider.For<FeatureRequestor>();
        private readonly Configuration _configuration;
        private readonly HttpClient _httpClient;
        private readonly Uri _uri;

        internal FeatureRequestor(Configuration config)
        {
            _httpClient = config.HttpClient;
            _configuration = config;
            _uri = new Uri(_configuration.BaseUri.AbsoluteUri + "sdk/latest-flags");
        }

        internal async Task<IDictionary<string, FeatureFlag>> MakeAllRequestAsync()
        {
            Logger.Debug("Getting all features with uri: " + _uri.AbsoluteUri);

            using (var responseTask = _httpClient.GetAsync(_uri))
            {
                var response = await responseTask.ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(content);
            }
        }
    }

}

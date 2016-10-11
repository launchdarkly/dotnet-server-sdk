using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal class FeatureRequestor
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<FeatureRequestor>();
        private readonly Uri _uri;
        private readonly Configuration _config;
        private EntityTagHeaderValue _etag;

        internal FeatureRequestor(Configuration config)
        {
            _config = config;
            _uri = new Uri(config.BaseUri.AbsoluteUri + "sdk/latest-flags");
        }

        // Returns a dictionary of the latest flags, or null if they have not been modified. Throws an exception if there
        // was a problem getting flags.
        internal async Task<IDictionary<string, FeatureFlag>> MakeAllRequestAsync()
        {
            using (var httpClient = _config.HttpClient())
            {
                Logger.LogDebug("Getting all flags with uri: " + _uri.AbsoluteUri);
                if (_etag != null)
                {
                    httpClient.DefaultRequestHeaders.IfNoneMatch.Clear();
                    httpClient.DefaultRequestHeaders.IfNoneMatch.Add(_etag);
                }
                using (var response = await httpClient.GetAsync(_uri).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        Logger.LogDebug("Get all flags returned 304: not modified");
                        return null;
                    }
                    _etag = response.Headers.ETag;
                    //We ensure the status code after checking for 304, because 304 isn't considered success
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var flags = JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(content);
                    Logger.LogDebug("Get all flags returned " + flags.Keys.Count + " feature flags");
                    return flags;
                }
            }
        }
    }
}
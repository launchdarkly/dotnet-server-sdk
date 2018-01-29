using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal class FeatureRequestor
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<FeatureRequestor>();
        private readonly Uri _uri;
        private volatile HttpClient _httpClient;
        private readonly Configuration _config;
        private volatile EntityTagHeaderValue _etag;

        internal FeatureRequestor(Configuration config)
        {
            _config = config;
            _uri = new Uri(config.BaseUri.AbsoluteUri + "sdk/latest-flags");
            _httpClient = config.HttpClient();
        }

        // Returns a dictionary of the latest flags, or null if they have not been modified. Throws an exception if there
        // was a problem getting flags.
        internal async Task<IDictionary<string, FeatureFlag>> GetAllFlagsAsync()
        {
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            string content = null;
            try
            {
                content = await Get(cts, _uri);
                if(string.IsNullOrEmpty(content)) {
                    return null;
                }
                var flags = JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(content);

                Logger.LogDebug("Get all flags returned {0} feature flags",
                    flags.Keys.Count);

                return flags;
            }
            catch (Exception e)
            {
                Logger.LogError(e, 
                    "Error getting feature flags: {0}",
                    Util.ExceptionMessage(e));

                Logger.LogDebug("{0}", e);
                return null;
            }
        }

        // Returns the latest version of a flag, or null if it has not been modified. Throws an exception if there
        // was a problem getting flags.
        internal async Task<FeatureFlag> GetFlagAsync(string featureKey)
        {
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            string content = null;
            Uri flagPath = new Uri(_uri + "/" + featureKey);
            try
            {
                content = await Get(cts, flagPath);
                var flag = JsonConvert.DeserializeObject<FeatureFlag>(content);
                return flag;
            }
            catch (Exception e)
            {
                Logger.LogDebug(e,
                    "Error getting feature flags: {0} waiting 1 second before retrying.",
                    Util.ExceptionMessage(e));

                System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                cts = new CancellationTokenSource(_config.HttpClientTimeout);
                try
                {
                    content = await Get(cts, flagPath);
                    var flag = JsonConvert.DeserializeObject<FeatureFlag>(content);
                    return flag;
                }
                catch (TaskCanceledException tce)
                {
                    if (tce.CancellationToken == cts.Token)
                    {
                        //Indicates the task was cancelled by something other than a request timeout
                        throw;
                    }
                    //Otherwise this was a request timeout.
                    throw new Exception("Get Feature with URL: " + flagPath + " timed out after : " +
                                        _config.HttpClientTimeout);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
        private async Task<string> Get(CancellationTokenSource cts, Uri path)
        {
            Logger.LogDebug("Getting flags with uri: {0}", path.AbsoluteUri);
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (_etag != null)
            {
                request.Headers.IfNoneMatch.Add(_etag);
            }

            using (var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    Logger.LogDebug("Get all flags returned 304: not modified");
                    return null;
                }
                _etag = response.Headers.ETag;
                //We ensure the status code after checking for 304, because 304 isn't considered success
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }
}
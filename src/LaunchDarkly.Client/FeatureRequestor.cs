using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Common.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal class FeatureRequestor : IFeatureRequestor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FeatureRequestor));
        private readonly Uri _allUri;
        private readonly Uri _flagsUri;
        private readonly Uri _segmentsUri;
        private volatile HttpClient _httpClient;
        private readonly Configuration _config;
        private volatile EntityTagHeaderValue _etag;

        internal FeatureRequestor(Configuration config)
        {
            _config = config;
            _allUri = new Uri(config.BaseUri.AbsoluteUri + "sdk/latest-all");
            _flagsUri = new Uri(config.BaseUri.AbsoluteUri + "sdk/latest-flags/");
            _segmentsUri = new Uri(config.BaseUri.AbsoluteUri + "sdk/latest-segments/");
            _httpClient = config.HttpClient();
        }

        // Returns a dictionary of the latest flags, or null if they have not been modified. Throws an exception if there
        // was a problem getting flags.
        async Task<AllData> IFeatureRequestor.GetAllDataAsync()
        {
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            string content = null;
            content = await Get(cts, _allUri);
            if (string.IsNullOrEmpty(content)) {
                return null;
            }
            var ret = JsonConvert.DeserializeObject<AllData>(content);

            Log.DebugFormat("Get all returned {0} feature flags and {1} segments",
                ret.Flags.Keys.Count, ret.Segments.Keys.Count);

            return ret;
        }

        // Returns the latest version of a flag, or null if it has not been modified. Throws an exception if there
        // was a problem getting flags.
        async Task<FeatureFlag> IFeatureRequestor.GetFlagAsync(string featureKey)
        {
            return await GetObjectAsync<FeatureFlag>(featureKey, "feature flag", typeof(FeatureFlag), _flagsUri);
        }

        // Returns the latest version of a segment, or null if it has not been modified. Throws an exception if there
        // was a problem getting segments.
        async Task<Segment> IFeatureRequestor.GetSegmentAsync(string segmentKey)
        {
            return await GetObjectAsync<Segment>(segmentKey, "segment", typeof(Segment), _segmentsUri);
        }

        internal async Task<T> GetObjectAsync<T>(string key, string objectName, Type objectType, Uri uriBase) where T : class
        {
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            string content = null;
            Uri apiPath = new Uri(uriBase + key);
            try
            {
                content = await Get(cts, apiPath);
                return (content == null) ? null : (T) JsonConvert.DeserializeObject(content, objectType);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Error getting {0}: {1} waiting 1 second before retrying.",
                    e, objectName, Util.ExceptionMessage(e));

                System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                cts = new CancellationTokenSource(_config.HttpClientTimeout);
                try
                {
                    content = await Get(cts, apiPath);
                    return (content == null) ? null : (T)JsonConvert.DeserializeObject(content, objectType);
                }
                catch (TaskCanceledException tce)
                {
                    if (tce.CancellationToken == cts.Token)
                    {
                        //Indicates the task was cancelled by something other than a request timeout
                        throw;
                    }
                    //Otherwise this was a request timeout.
                    throw new TimeoutException("Get item with URL: " + apiPath +
                                                " timed out after : " + _config.HttpClientTimeout);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private async Task<string> Get(CancellationTokenSource cts, Uri path)
        {
            Log.DebugFormat("Getting flags with uri: {0}", path.AbsoluteUri);
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (_etag != null)
            {
                request.Headers.IfNoneMatch.Add(_etag);
            }

            using (var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    Log.Debug("Get all flags returned 304: not modified");
                    return null;
                }
                _etag = response.Headers.ETag;
                //We ensure the status code after checking for 304, because 304 isn't considered success
                if (!response.IsSuccessStatusCode)
                {
                    throw new FeatureRequestorUnsuccessfulResponseException((int)response.StatusCode);
                }
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }
}
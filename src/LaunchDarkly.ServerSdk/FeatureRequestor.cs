using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Common.Logging;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    internal class FeatureRequestor : IFeatureRequestor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FeatureRequestor));
        private readonly Uri _allUri;
        private readonly HttpClient _httpClient;
        private readonly Configuration _config;
        private readonly Dictionary<Uri, EntityTagHeaderValue> _etags = new Dictionary<Uri, EntityTagHeaderValue>();

        internal FeatureRequestor(Configuration config, Uri baseUri)
        {
            _config = config;
            _allUri = new Uri(baseUri.AbsoluteUri + "sdk/latest-all");
            _httpClient = Util.MakeHttpClient(config.HttpRequestConfiguration, ServerSideClientEnvironment.Instance);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
        }

        // Returns a dictionary of the latest flags, or null if they have not been modified. Throws an exception if there
        // was a problem getting flags.
        public async Task<AllData> GetAllDataAsync()
        {
            var ret = await GetAsync<AllData>(_allUri);
            if (ret != null)
            {
                Log.DebugFormat("Get all returned {0} feature flags and {1} segments",
                    ret.Flags.Keys.Count, ret.Segments.Keys.Count);
            }
            return ret;
        }

        private async Task<T> GetAsync<T>(Uri path) where T : class
        {
            Log.DebugFormat("Getting flags with uri: {0}", path.AbsoluteUri);
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            lock (_etags)
            {
                if (_etags.TryGetValue(path, out var etag))
                {
                    request.Headers.IfNoneMatch.Add(etag);
                }
            }

            using (var cts = new CancellationTokenSource(_config.HttpClientTimeout))
            {
                try
                {
                    using (var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.NotModified)
                        {
                            Log.Debug("Get all flags returned 304: not modified");
                            return null;
                        }
                        //We ensure the status code after checking for 304, because 304 isn't considered success
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new UnsuccessfulResponseException((int)response.StatusCode);
                        }
                        lock (_etags)
                        {
                            if (response.Headers.ETag != null)
                            {
                                _etags[path] = response.Headers.ETag;
                            }
                            else
                            {
                                _etags.Remove(path);
                            }
                        }
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return string.IsNullOrEmpty(content) ? null : JsonUtil.DecodeJson<T>(content);
                    }
                }
                catch (TaskCanceledException tce)
                {
                    if (tce.CancellationToken == cts.Token)
                    {
                        //Indicates the task was cancelled by something other than a request timeout
                        throw;
                    }
                    //Otherwise this was a request timeout.
                    throw new TimeoutException("Get item with URL: " + path.AbsoluteUri +
                                                " timed out after : " + _config.HttpClientTimeout);
                }
            }
        }
    }
}

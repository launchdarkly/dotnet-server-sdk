using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LaunchDarkly.JsonStream;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal class FeatureRequestor : IFeatureRequestor
    {
        private readonly Uri _allUri;
        private readonly HttpClient _httpClient;
        private readonly HttpProperties _httpProperties;
        private readonly TimeSpan _connectTimeout;
        private readonly Dictionary<Uri, EntityTagHeaderValue> _etags = new Dictionary<Uri, EntityTagHeaderValue>();
        private readonly Logger _log;

        internal FeatureRequestor(LdClientContext context, Uri baseUri)
        {
            _httpProperties = context.Http.HttpProperties;
            _httpClient = context.Http.NewHttpClient();
            _connectTimeout = context.Http.ConnectTimeout;
            _allUri = baseUri.AddPath(StandardEndpoints.PollingRequestPath);
            _log = context.Basic.Logger.SubLogger(LogNames.DataSourceSubLog);
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

        // Returns a data set of the latest flags and segments, or null if they have not been modified. Throws an
        // exception if there was a problem getting data.
        public async Task<FullDataSet<ItemDescriptor>?> GetAllDataAsync()
        {
            var json = await GetAsync(_allUri);
            if (json is null)
            {
                return null;
            }
            var data = ParseAllData(json);
            Func<DataKind, int> countItems = kind =>
                data.Data.FirstOrDefault(kv => kv.Key == kind).Value.Items?.Count() ?? 0;
            _log.Debug("Get all returned {0} feature flags and {1} segments",
                countItems(DataModel.Features), countItems(DataModel.Segments));
            return data;
        }

        private FullDataSet<ItemDescriptor> ParseAllData(string json)
        {
            var r = JReader.FromString(json);
            return StreamProcessorEvents.ParseFullDataset(ref r);
        }

        private async Task<string> GetAsync(Uri path)
        {
            _log.Debug("Getting flags with uri: {0}", path.AbsoluteUri);
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            _httpProperties.AddHeaders(request);
            lock (_etags)
            {
                if (_etags.TryGetValue(path, out var etag))
                {
                    request.Headers.IfNoneMatch.Add(etag);
                }
            }

            using (var cts = new CancellationTokenSource(_connectTimeout))
            {
                try
                {
                    using (var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.NotModified)
                        {
                            _log.Debug("Get all flags returned 304: not modified");
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
                        return string.IsNullOrEmpty(content) ? null : content;
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
                                                " timed out after : " + _connectTimeout);
                }
            }
        }
    }
}

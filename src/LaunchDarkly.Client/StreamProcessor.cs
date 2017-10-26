using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using LaunchDarkly.EventSource;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    internal class StreamProcessor : IUpdateProcessor
    {
        private const String PUT = "put";
        private const String PATCH = "patch";
        private const String DELETE = "delete";
        private const String INDIRECT_PATCH = "indirect/patch";
        private static readonly ILogger Logger = LdLogger.CreateLogger<StreamProcessor>();
        private static int UNINITIALIZED = 0;
        private static int INITIALIZED = 1;
        private readonly Configuration _config;
        private readonly FeatureRequestor _featureRequestor;
        private readonly IFeatureStore _featureStore;
        private int _initialized = UNINITIALIZED;
        private readonly TaskCompletionSource<bool> _initTask;
        private static EventSource.EventSource _es;

        internal StreamProcessor(Configuration config, FeatureRequestor featureRequestor, IFeatureStore featureStore)
        {
            _config = config;
            _featureRequestor = featureRequestor;
            _featureStore = featureStore;
            _initTask = new TaskCompletionSource<bool>();
        }

        bool IUpdateProcessor.Initialized()
        {
            return _initialized == INITIALIZED;
        }

        async Task<bool> IUpdateProcessor.Start()
        {
            Dictionary<string, string> headers = new Dictionary<string, string> { { "Authorization", _config.SdkKey }, { "User-Agent", "DotNetClient/" + Configuration.Version }, { "Accept", "text/event-stream" } };

            EventSource.Configuration config = new EventSource.Configuration(
                uri: new Uri(_config.StreamUri, "/flags"),
                messageHandler: _config.HttpClientHandler,
                connectionTimeOut: _config.HttpClientTimeout,
                delayRetryDuration: _config.ReconnectTime,
                readTimeout: _config.ReadTimeout,
                requestHeaders: headers,
                logger: LdLogger.CreateLogger<EventSource.EventSource>()
            );
            _es = new EventSource.EventSource(config);

            _es.CommentReceived += OnComment;
            _es.MessageReceived += OnMessage;
            _es.Error += OnError;
            _es.Opened += OnOpen;
            _es.Closed += OnClose;

            try
            {
                await _es.StartAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("General Exception: {0}", ex);
                _initTask.SetException(ex);
            }
            return await _initTask.Task;
        }

        private async void RestartEventSource()
        {
            Logger.LogInformation("Stopping LaunchDarkly StreamProcessor");
            _es.Close();
            try
            {
                await _es.StartAsync();
                Logger.LogInformation("Reconnected to LaunchDarkly StreamProcessor");
            }
            catch (Exception exc)
            {
                Logger.LogError("General Exception: {0}", exc);
            }
        }

        private async void OnMessage(object sender, EventSource.MessageReceivedEventArgs e)
        {
            try
            {
                switch (e.EventName)
                {
                case PUT:
                    _featureStore.Init(JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(e.Message.Data));
                    if (Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0)
                    {
                        _initTask.SetResult(true);
                        Logger.LogInformation("Initialized LaunchDarkly Stream Processor.");
                    }
                    break;
                case PATCH:
                    FeaturePatchData patchData = JsonConvert.DeserializeObject<FeaturePatchData>(e.Message.Data);
                    _featureStore.Upsert(patchData.Key(), patchData.Data);
                    break;
                case DELETE:
                    FeatureDeleteData deleteData = JsonConvert.DeserializeObject<FeatureDeleteData>(e.Message.Data);
                    _featureStore.Delete(deleteData.Key(), deleteData.Version);
                    break;
                case INDIRECT_PATCH:
                    await UpdateTaskAsync(e.Message.Data);
                    break;
                }
            }
            catch (JsonReaderException ex)
            {
                Logger.LogDebug("Failed to deserialize feature flag {0}, waiting 1 second before reconnecting:\n{1}", e.EventName, e.Message.Data);
                Logger.LogError("Encountered an error reading feature flag configuration: {0}", ex);
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                RestartEventSource();
            }
            catch (Exception ex)
            {
                Logger.LogError("Encountered an unexpected error, waiting 1 second before reconnecting:", ex);
                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                RestartEventSource();
            }
        }
        private void OnOpen(object sender, EventSource.StateChangedEventArgs e)
        {
            Logger.LogDebug("Eventsource Opened");
        }

        private void OnClose(object sender, EventSource.StateChangedEventArgs e)
        {
            Logger.LogDebug("Eventsource Closed");
        }

        private void OnComment(object sender, EventSource.CommentReceivedEventArgs e)
        {
            Logger.LogDebug("Received a heartbeat.");
        }

        private void OnError(object sender, EventSource.ExceptionEventArgs e)
        {
            Logger.LogError("Encountered EventSource error:", e.Exception.Message);
            Logger.LogDebug("", e);
        }

        void IDisposable.Dispose()
        {
            Logger.LogInformation("Stopping LaunchDarkly StreamProcessor");
            _es.Close();
        }

        private async Task UpdateTaskAsync(string featureKey)
        {
            try
            {
                var feature = await _featureRequestor.GetFlagAsync(featureKey);
                if (feature != null)
                {
                    _featureStore.Upsert(featureKey, feature);
                }
            }
            catch (AggregateException ex)
            {
                Logger.LogError(string.Format("Error Updating feature: '{0}'", Util.ExceptionMessage(ex.Flatten())));
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Error Updating feature: '{0}'", Util.ExceptionMessage(ex)));
            }
        }
        internal class FeaturePatchData
        {
            internal string Path { get; private set; }
            internal FeatureFlag Data { get; private set; }

            [JsonConstructor]
            internal FeaturePatchData(string path, FeatureFlag data)
            {
                Path = path;
                Data = data;
            }

            public String Key()
            {
                return Path.Substring(1);
            }
        }

        internal class FeatureDeleteData
        {
            internal string Path { get; private set; }
            internal int Version { get; private set; }

            [JsonConstructor]
            internal FeatureDeleteData(string path, int version)
            {
                Path = path;
                Version = version;
            }

            public String Key()
            {
                return Path.Substring(1);
            }
        }
    }
}

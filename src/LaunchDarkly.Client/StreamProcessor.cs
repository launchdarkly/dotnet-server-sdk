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
        private const String INDIRECT_PUT = "indirect/put";
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

        TaskCompletionSource<bool> IUpdateProcessor.Start()
        {
            Dictionary<string, string> headers = new Dictionary<string, string> {{"Authorization", _config.SdkKey}, {"User-Agent", "DotNetClient/" + Configuration.Version}, {"Accept", "text/event-stream"}};
            
            EventSource.Configuration config = new EventSource.Configuration(
                uri: new Uri(_config.StreamUri, "/flags"),
                connectionTimeOut: _config.ConnectionTimeout,
                delayRetryDuration: _config.ReconnectTime,
                readTimeout: _config.ReadTimeout,
                requestHeaders: headers,
                logger: LdLogger.CreateLogger<EventSource.EventSource>()
            );
            _es = new EventSource.EventSource(config);

            _es.Opened += OnOpen;
            _es.Closed += OnClosed;
            _es.CommentReceived += OnComment;
            _es.MessageReceived += OnMessage;
            _es.Error += OnError;

            try
            {
                _es.StartAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("General Exception: {0}", ex);
            }
            return _initTask;
        }

        private void OnOpen(object sender, EventSource.StateChangedEventArgs e)
        {
        }

        private void OnClosed(object sender, EventSource.StateChangedEventArgs e)
        {
        }

        private void OnMessage(object sender, EventSource.MessageReceivedEventArgs e)
        {
            switch(e.EventName)
            {
                case PUT:
                    _featureStore.Init(JsonConvert.DeserializeObject<IDictionary<string, FeatureFlag>>(e.Message.Data));
                    if(Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0) {
                        _initTask.SetResult(true);
                        Logger.LogInformation("Initialized LaunchDarkly Stream Processor.");
                    }
                    break;
                case PATCH:
                    FeaturePatchData patchData = JsonConvert.DeserializeObject<FeaturePatchData>(e.Message.Data);
                    _featureStore.Upsert(patchData.Key(), patchData.Feature());
                    break;
                case DELETE:
                    FeatureDeleteData deleteData = JsonConvert.DeserializeObject<FeatureDeleteData>(e.Message.Data);
                    _featureStore.Delete(deleteData.Key(), deleteData.Version());
                    break;
                case INDIRECT_PUT:
                    InitTaskAsync();
                    break;
                case INDIRECT_PATCH:
                    UpdateTaskAsync(e.Message.Data);
                    break;
            }
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

        private async Task InitTaskAsync()
        {
            try
            {
                var allFeatures = await _featureRequestor.GetAllFlagsAsync();
                if (allFeatures != null)
                {
                    _featureStore.Init(allFeatures);

                    //We can't use bool in CompareExchange because it is not a reference type.
                    if (Interlocked.CompareExchange(ref _initialized, INITIALIZED, UNINITIALIZED) == 0)
                    {
                        _initTask.SetResult(true);
                        Logger.LogInformation("Initialized LaunchDarkly Streaming Processor.");
                    }
                }
            }
            catch (AggregateException ex)
            {
                Logger.LogError(string.Format("Error Initializing features: '{0}'", Util.ExceptionMessage(ex.Flatten())));
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Error Initializing features: '{0}'", Util.ExceptionMessage(ex)));
            }
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
        private class FeaturePatchData
        {
            string path = "";
            FeatureFlag data = null;

            public FeaturePatchData() {}

            public String Key() {
                return path.Substring(1);
            }

            public FeatureFlag Feature() {
                return data;
            }
        }

        private class FeatureDeleteData
        {
            string path = "";
            int version = 0;

            public FeatureDeleteData() {}

            public String Key() {
                return path.Substring(1);
            }

            public int Version() {
                return version;
            }
        }
    }
}
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
        private readonly EventSource.ExponentialBackoffWithDecorrelation _backOff;

        internal StreamProcessor(Configuration config, FeatureRequestor featureRequestor, IFeatureStore featureStore)
        {
            _config = config;
            _featureRequestor = featureRequestor;
            _featureStore = featureStore;
            _initTask = new TaskCompletionSource<bool>();
            _backOff = new EventSource.ExponentialBackoffWithDecorrelation(_config.ReconnectTime, TimeSpan.FromMilliseconds(30000));
        }

        bool IUpdateProcessor.Initialized()
        {
            return _initialized == INITIALIZED;
        }

        Task<bool> IUpdateProcessor.Start()
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
                Task.Run(() => _es.StartAsync());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "General Exception: {0}",
                    Util.ExceptionMessage(ex));

                _initTask.SetException(ex);
            }
            return _initTask.Task;
        }

        private async void RestartEventSource()
        {
            TimeSpan sleepTime = TimeSpan.FromMilliseconds(0);
            if (_backOff.GetReconnectAttemptCount() > 0 && _config.ReconnectTime > TimeSpan.FromMilliseconds(0))
            {
                sleepTime = _backOff.GetNextBackOff();

                Logger.LogInformation("Stopping LaunchDarkly StreamProcessor. Waiting {0} milliseconds before reconnecting...",
                    sleepTime.TotalMilliseconds);
            }
            else
            {
                _backOff.IncrementReconnectAttemptCount();
            }
            _es.Close();
            await Task.Delay(sleepTime);
            try
            {
                await _es.StartAsync();
                _backOff.ResetReconnectAttemptCount();
                Logger.LogInformation("Reconnected to LaunchDarkly StreamProcessor");
            }
            catch (Exception exc)
            {
                Logger.LogError(exc,
                    "General Exception: {0}",
                    Util.ExceptionMessage(exc));
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
                Logger.LogDebug(ex,
                    "Failed to deserialize feature flag {0}:\n{1}",
                    e.EventName,
                    e.Message.Data);

                Logger.LogError(ex,
                    "Encountered an error reading feature flag configuration: {0}",
                    Util.ExceptionMessage(ex));

                RestartEventSource();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Encountered an unexpected error: {0}",
                    Util.ExceptionMessage(ex));

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
            Logger.LogError(e.Exception,
                "Encountered EventSource error: {0}",
                Util.ExceptionMessage(e.Exception));
            if (e.Exception is EventSource.EventSourceServiceUnsuccessfulResponseException)
            {
                if (((EventSource.EventSourceServiceUnsuccessfulResponseException)e.Exception).StatusCode == 401)
                {
                    Logger.LogError("Received 401 error, no further streaming connection will be made since SDK key is invalid");
                    ((IDisposable)this).Dispose();
                }
            }
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
                Logger.LogError(ex,
                    "Error Updating feature: '{0}'",
                    Util.ExceptionMessage(ex.Flatten()));
            }
            catch (FeatureRequestorUnsuccessfulResponseException ex) when (ex.StatusCode == 401)
            {
                Logger.LogError(string.Format("Error Updating feature: '{0}'", Util.ExceptionMessage(ex)));
                if (ex.StatusCode == 401)
                {
                    Logger.LogError("Received 401 error, no further streaming connection will be made since SDK key is invalid");
                    ((IDisposable)this).Dispose();
                }
            }
            catch (TimeoutException ex) {
                Logger.LogError(ex,
                    "Error Updating feature: '{0}'",
                    Util.ExceptionMessage(ex));
                RestartEventSource();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Error Updating feature: '{0}'",
                    Util.ExceptionMessage(ex));
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

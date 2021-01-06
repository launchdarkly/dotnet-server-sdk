using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using LaunchDarkly.JsonStream;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.DataSources.StreamProcessorEvents;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal class StreamProcessor : IDataSource
    {
        // The read timeout for the stream is not the same read timeout that can be set in the SDK configuration.
        // It is a fixed value that is set to be slightly longer than the expected interval between heartbeats
        // from the LaunchDarkly streaming server. If this amount of time elapses with no new data, the connection
        // will be cycled.
        private static readonly TimeSpan LaunchDarklyStreamReadTimeout = TimeSpan.FromMinutes(5);

        private const String PUT = "put";
        private const String PATCH = "patch";
        private const String DELETE = "delete";

        private readonly IDataSourceUpdates _dataSourceUpdates;
        private readonly IHttpConfiguration _httpConfig;
        private readonly TimeSpan _initialReconnectDelay;
        private readonly TaskCompletionSource<bool> _initTask;
        private readonly EventSourceCreator _eventSourceCreator;
        private readonly EventSource.ExponentialBackoffWithDecorrelation _backoff;
        private readonly IDiagnosticStore _diagnosticStore;
        private readonly AtomicBoolean _initialized = new AtomicBoolean(false);
        private readonly Uri _streamUri;
        private readonly bool _storeStatusMonitoringEnabled;
        private readonly Logger _log;

        private volatile IEventSource _es;
        private volatile bool _lastStoreUpdateFailed = false;
        internal DateTime _esStarted; // exposed for testing

        internal delegate IEventSource EventSourceCreator(Uri streamUri,
            IHttpConfiguration httpConfig);

        internal StreamProcessor(
            LdClientContext context,
            IDataSourceUpdates dataSourceUpdates,
            Uri baseUri,
            TimeSpan initialReconnectDelay,
            EventSourceCreator eventSourceCreator
            )
        {
            _log = context.Basic.Logger.SubLogger(LogNames.DataSourceSubLog);

            _dataSourceUpdates = dataSourceUpdates;
            _httpConfig = context.Http;
            _initialReconnectDelay = initialReconnectDelay;
            _diagnosticStore = context.DiagnosticStore;
            _eventSourceCreator = eventSourceCreator ?? CreateEventSource;
            _initTask = new TaskCompletionSource<bool>();
            _backoff = new EventSource.ExponentialBackoffWithDecorrelation(initialReconnectDelay,
                EventSource.Configuration.MaximumRetryDuration);
            _streamUri = new Uri(baseUri, "/all");

            _storeStatusMonitoringEnabled = _dataSourceUpdates.DataStoreStatusProvider.StatusMonitoringEnabled;
            if (_storeStatusMonitoringEnabled)
            {
                _dataSourceUpdates.DataStoreStatusProvider.StatusChanged += OnDataStoreStatusChanged;
            }
        }

        #region IDataSource

        public bool Initialized => _initialized.Get();

        public Task<bool> Start()
        {
            _es = _eventSourceCreator(_streamUri, _httpConfig);

            _es.MessageReceived += OnMessage;
            _es.Error += OnError;
            _es.Opened += OnOpen;

            Task.Run(() => {
                _esStarted = DateTime.Now;
                return _es.StartAsync();
            });

            return _initTask.Task;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _log.Info("Stopping LaunchDarkly StreamProcessor");
                if (_es != null)
                {
                    _es.Close();
                }
                if (_storeStatusMonitoringEnabled)
                {
                    _dataSourceUpdates.DataStoreStatusProvider.StatusChanged -= OnDataStoreStatusChanged;
                }
            }
        }

        #endregion

        private IEventSource CreateEventSource(Uri uri, IHttpConfiguration httpConfig)
        {
            var configBuilder = EventSource.Configuration.Builder(uri)
                .Method(HttpMethod.Get)
                .MessageHandler(httpConfig.MessageHandler)
                .ConnectionTimeout(httpConfig.ConnectTimeout)
                .DelayRetryDuration(_initialReconnectDelay)
                .ReadTimeout(LaunchDarklyStreamReadTimeout)
                .RequestHeaders(httpConfig.DefaultHeaders.ToDictionary(kv => kv.Key, kv => kv.Value))
                .Logger(_log);
            return new EventSource.EventSource(configBuilder.Build());
        }

        private void Restart()
        {
            TimeSpan sleepTime = _backoff.GetNextBackOff();
            if (sleepTime != TimeSpan.Zero)
            {
                _log.Info("Restarting stream. Waiting {0} milliseconds before reconnecting...",
                    sleepTime.TotalMilliseconds);
            }
            _es.Close();

            // Everything after this point is async and done in the background - Restart returns immediately after the Close
            _ = FinishRestart(sleepTime);
        }

        private async Task FinishRestart(TimeSpan sleepTime)
        {
            await Task.Delay(sleepTime);
            try
            {
                _esStarted = DateTime.Now;
                await _es.StartAsync();
                _backoff.ResetReconnectAttemptCount();
                _log.Info("Reconnected to LaunchDarkly stream");
            }
            catch (Exception ex)
            {
                LogHelpers.LogException(_log, null, ex);
            }
        }

        private void RecordStreamInit(bool failed)
        {
            if (_diagnosticStore != null)
            {
                DateTime now = DateTime.Now;
                _diagnosticStore.AddStreamInit(_esStarted, now - _esStarted, failed);
                _esStarted = now;
            }
        }

        private void OnOpen(object sender, EventSource.StateChangedEventArgs e)
        {
            _log.Debug("EventSource Opened");
            RecordStreamInit(false);
        }

        private void OnMessage(object sender, EventSource.MessageReceivedEventArgs e)
        {
            try
            {
                HandleMessage(e.EventName, e.Message.Data);
            }
            catch (JsonReadException ex)
            {
                _log.Error("LaunchDarkly service request failed or received invalid data: {0}",
                    LogValues.ExceptionSummary(ex));

                var errorInfo = new DataSourceStatus.ErrorInfo
                {
                    Kind = DataSourceStatus.ErrorKind.InvalidData,
                    Message = ex.Message,
                    Time = DateTime.Now
                };
                _dataSourceUpdates.UpdateStatus(DataSourceState.Interrupted, errorInfo);

                Restart();
            }
            catch (StreamStoreException)
            {
                if (!_storeStatusMonitoringEnabled)
                {
                    if (!_lastStoreUpdateFailed)
                    {
                        _log.Warn("Restarting stream to ensure that we have the latest data");
                    }
                    Restart();
                }
                _lastStoreUpdateFailed = true;
            }
            catch (Exception ex)
            {
                LogHelpers.LogException(_log, "Unexpected error in stream processing", ex);
                Restart();
            }
        }

        private void OnError(object sender, EventSource.ExceptionEventArgs e)
        {
            var ex = e.Exception;
            LogHelpers.LogException(_log, "Encountered EventSource error", ex);
            var recoverable = true;
            if (ex is EventSource.EventSourceServiceUnsuccessfulResponseException respEx)
            {
                int status = respEx.StatusCode;
                _log.Error(HttpErrors.ErrorMessage(status, "streaming connection", "will retry"));
                RecordStreamInit(true);
                if (!HttpErrors.IsRecoverable(status))
                {
                    recoverable = false;
                    _initTask.TrySetException(ex); // sends this exception to the client if we haven't already started up
                    ((IDisposable)this).Dispose();
                }
            }

            var errorInfo = ex is EventSource.EventSourceServiceUnsuccessfulResponseException re ?
                DataSourceStatus.ErrorInfo.FromHttpError(re.StatusCode) :
                DataSourceStatus.ErrorInfo.FromException(ex);
            _dataSourceUpdates.UpdateStatus(recoverable ? DataSourceState.Interrupted : DataSourceState.Off,
                errorInfo);
        }

        private void HandleMessage(string messageType, string messageData)
        {
            switch (messageType)
            {
                case PUT:
                    var putData = ParsePutData(messageData);
                    if (!_dataSourceUpdates.Init(putData.Data))
                    {
                        throw new StreamStoreException("failed to write full data set to data store");
                    }
                    _lastStoreUpdateFailed = false;
                    _dataSourceUpdates.UpdateStatus(DataSourceState.Valid, null);
                    if (!_initialized.GetAndSet(true))
                    {
                        _initTask.SetResult(true);
                        _log.Info("Initialized LaunchDarkly Stream Processor.");
                    }
                    break;

                case PATCH:
                    PatchData patchData = ParsePatchData(messageData);
                    if (patchData.Kind is null)
                    {
                        _log.Warn("Received patch event with unknown path");
                    }
                    else
                    {
                        if (!_dataSourceUpdates.Upsert(patchData.Kind, patchData.Key, patchData.Item))
                        {
                            throw new StreamStoreException(string.Format("failed to update \"{0}\" ({1}) in data store",
                                patchData.Key, patchData.Kind.Name));
                        }
                    }
                    _lastStoreUpdateFailed = false;
                    break;

                case DELETE:
                    DeleteData deleteData = ParseDeleteData(messageData);
                    if (deleteData.Kind is null)
                    {
                        _log.Warn("Received patch event with unknown path");
                    }
                    else
                    {
                        var tombstone = new ItemDescriptor(deleteData.Version, null);
                        if (!_dataSourceUpdates.Upsert(deleteData.Kind, deleteData.Key, tombstone))
                        {
                            throw new StreamStoreException(string.Format("failed to delete \"{0}\" ({1}) in data store",
                                deleteData.Key, deleteData.Kind.Name));
                        }
                        _lastStoreUpdateFailed = false;
                    }
                    break;
            }
        }

        private void OnDataStoreStatusChanged(object sender, DataStoreStatus newStatus)
        {
            if (newStatus.Available && newStatus.RefreshNeeded)
            {
                // The store has just transitioned from unavailable to available, and we can't guarantee that
                // all of the latest data got cached, so let's restart the stream to refresh all the data.
                _log.Warn("Restarting stream to refresh data after data store outage");
                Restart();
            }
        }

        private sealed class StreamStoreException : Exception
        {
            public StreamStoreException(string message) : base(message) { }
        }
    }
}

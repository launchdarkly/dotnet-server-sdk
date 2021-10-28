using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LaunchDarkly.EventSource;
using LaunchDarkly.JsonStream;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Concurrent;
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
        private readonly HttpConfiguration _httpConfig;
        private readonly TimeSpan _initialReconnectDelay;
        private readonly TaskCompletionSource<bool> _initTask;
        private readonly IDiagnosticStore _diagnosticStore;
        private readonly AtomicBoolean _initialized = new AtomicBoolean(false);
        private readonly Uri _streamUri;
        private readonly bool _storeStatusMonitoringEnabled;
        private readonly Logger _log;

        private readonly IEventSource _es;
        private volatile bool _lastStoreUpdateFailed = false;
        internal DateTime _esStarted; // exposed for testing

        internal delegate IEventSource EventSourceCreator(Uri streamUri,
            HttpConfiguration httpConfig);

        internal StreamProcessor(
            LdClientContext context,
            IDataSourceUpdates dataSourceUpdates,
            Uri baseUri,
            TimeSpan initialReconnectDelay,
            EventSourceCreator eventSourceCreator
            )
        {
            _log = context.Basic.Logger.SubLogger(LogNames.DataSourceSubLog);
            _log.Info("Connecting to LaunchDarkly stream");

            _dataSourceUpdates = dataSourceUpdates;
            _httpConfig = context.Http;
            _initialReconnectDelay = initialReconnectDelay;
            _diagnosticStore = context.DiagnosticStore;
            _initTask = new TaskCompletionSource<bool>();
            _streamUri = baseUri.AddPath(StandardEndpoints.StreamingRequestPath);

            _storeStatusMonitoringEnabled = _dataSourceUpdates.DataStoreStatusProvider.StatusMonitoringEnabled;
            if (_storeStatusMonitoringEnabled)
            {
                _dataSourceUpdates.DataStoreStatusProvider.StatusChanged += OnDataStoreStatusChanged;
            }

            _es = (eventSourceCreator ?? CreateEventSource)(_streamUri, _httpConfig);
            _es.MessageReceived += OnMessage;
            _es.Error += OnError;
            _es.Opened += OnOpen;
        }

        #region IDataSource

        public bool Initialized => _initialized.Get();

        public Task<bool> Start()
        {
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
                _es.Close();
                if (_storeStatusMonitoringEnabled)
                {
                    _dataSourceUpdates.DataStoreStatusProvider.StatusChanged -= OnDataStoreStatusChanged;
                }
            }
        }

        #endregion

        private IEventSource CreateEventSource(Uri uri, HttpConfiguration httpConfig)
        {
            var configBuilder = EventSource.Configuration.Builder(uri)
                .Method(HttpMethod.Get)
                .HttpMessageHandler(httpConfig.HttpProperties.NewHttpMessageHandler())
                .ResponseStartTimeout(httpConfig.ResponseStartTimeout)
                .InitialRetryDelay(_initialReconnectDelay)
                .ReadTimeout(LaunchDarklyStreamReadTimeout)
                .RequestHeaders(httpConfig.DefaultHeaders.ToDictionary(kv => kv.Key, kv => kv.Value))
                .PreferDataAsUtf8Bytes(true) // See StreamProcessorEvents
                .Logger(_log);
            return new EventSource.EventSource(configBuilder.Build());
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
                HandleMessage(e.EventName, e.Message.DataUtf8Bytes.Data);
                // The way the PreferDataAsUtf8Bytes option works in EventSource is that if the
                // stream really is using UTF-8 encoding, the event data is passed to us directly
                // in Message.DataUtf8Bytes as a byte array and does not need to be converted to
                // a string. If the stream is for some reason using a different encoding, then
                // EventSource reads the data as a string (automatically converted by .NET from
                // whatever the encoding was), and then calling Message.DataUtf8Bytes converts
                // that to UTF-8 bytes.
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

                _es.Restart(false);
            }
            catch (StreamStoreException)
            {
                if (!_storeStatusMonitoringEnabled)
                {
                    if (!_lastStoreUpdateFailed)
                    {
                        _log.Warn("Restarting stream to ensure that we have the latest data");
                    }
                    _es.Restart(false);
                }
                _lastStoreUpdateFailed = true;
            }
            catch (Exception ex)
            {
                LogHelpers.LogException(_log, "Unexpected error in stream processing", ex);
                _es.Restart(false);
            }
        }

        private void OnError(object sender, EventSource.ExceptionEventArgs e)
        {
            var ex = e.Exception;
            var recoverable = true;
            DataSourceStatus.ErrorInfo errorInfo;

            if (ex is EventSourceServiceUnsuccessfulResponseException respEx)
            {
                int status = respEx.StatusCode;
                errorInfo = DataSourceStatus.ErrorInfo.FromHttpError(status);
                RecordStreamInit(true);
                if (!HttpErrors.IsRecoverable(status))
                {
                    recoverable = false;
                    _log.Error(HttpErrors.ErrorMessage(status, "streaming connection", ""));
                }
                else
                {
                    _log.Warn(HttpErrors.ErrorMessage(status, "streaming connection", "will retry"));
                }
            }
            else
            {
                errorInfo = DataSourceStatus.ErrorInfo.FromException(ex);
                _log.Warn("Encountered EventSource error: {0}", LogValues.ExceptionSummary(ex));
                _log.Debug(LogValues.ExceptionTrace(ex));
            }

            _dataSourceUpdates.UpdateStatus(recoverable ? DataSourceState.Interrupted : DataSourceState.Off,
                errorInfo);

            if (!recoverable)
            {
                // Make _initTask complete to tell the client to stop waiting for initialization. We use
                // TrySetResult rather than SetResult here because it might have already been completed
                // (if for instance the stream started successfully, then restarted and got a 401).
                _initTask.TrySetResult(false);
                ((IDisposable)this).Dispose();
            }
        }

        private void HandleMessage(string messageType, byte[] messageData)
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
                        _initTask.TrySetResult(true);
                        _log.Info("LaunchDarkly streaming is active");
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
                _es.Restart(false);
            }
        }

        private sealed class StreamStoreException : Exception
        {
            public StreamStoreException(string message) : base(message) { }
        }
    }
}

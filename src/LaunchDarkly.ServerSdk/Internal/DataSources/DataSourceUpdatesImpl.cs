using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    /// <summary>
    /// The data source will push updates into this component. We then apply any necessary
    /// transformations before putting them into the data store; currently that just means sorting
    /// the data set for Init().
    /// </summary>
    /// <remarks>
    /// This component is also responsible for receiving updates to the data source status, broadcasting
    /// them to any status listeners, and tracking the length of any period of sustained failure.
    /// </remarks>
    internal class DataSourceUpdatesImpl : IDataSourceUpdates
    {
        private readonly IDataStore _store;
        private readonly TaskExecutor _taskExecutor;
        internal readonly Logger _log;
        private readonly OutageTracker _outageTracker;
        private readonly MultiNotifier _stateChangedSignal = new MultiNotifier();
        private readonly object _stateLock = new object();

        private DataSourceStatus _currentStatus;
        private volatile bool _lastStoreUpdateFailed = false;

        internal DataSourceStatus LastStatus
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentStatus;
                }
            }
        }

        internal event EventHandler<DataSourceStatus> StatusChanged;

        internal DataSourceUpdatesImpl(
            IDataStore store,
            TaskExecutor taskExecutor,
            Logger baseLogger,
            TimeSpan? outageLoggingTimeout
            )
        {
            _store = store;
            _taskExecutor = taskExecutor;
            _log = baseLogger.SubLogger(LogNames.DataSourceSubLog);
            _outageTracker = outageLoggingTimeout.HasValue ?
                new OutageTracker(_log, outageLoggingTimeout.Value) : null;
            _currentStatus = new DataSourceStatus
            {
                State = DataSourceState.Initializing,
                StateSince = DateTime.Now,
                LastError = null
            };
        }

        public bool Init(FullDataSet<ItemDescriptor> allData)
        {
            try
            {
                _store.Init(DataStoreSorter.SortAllCollections(allData));
            }
            catch (Exception e)
            {
                ReportStoreFailure(e);
                return false;
            }

            return true;
        }

        public void Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            try
            {
                _store.Upsert(kind, key, item);
            }
            catch (Exception e)
            {
                ReportStoreFailure(e);
            }
        }

        public void UpdateStatus(DataSourceState newState, DataSourceStatus.ErrorInfo? newError)
        {
            DataSourceStatus? statusToBroadcast = null;

            lock (_stateLock)
            {
                var oldStatus = _currentStatus;

                if (newState == DataSourceState.Interrupted && oldStatus.State == DataSourceState.Initializing)
                {
                    newState = DataSourceState.Initializing; // see comment on IDataSourceUpdates.UpdateStatus
                }

                if (newState != oldStatus.State || newError.HasValue)
                {
                    _currentStatus = new DataSourceStatus
                    {
                        State = newState,
                        StateSince = newState == oldStatus.State ? oldStatus.StateSince : DateTime.Now,
                        LastError = newError.HasValue ? newError : oldStatus.LastError
                    };
                    statusToBroadcast = _currentStatus;
                    _stateChangedSignal.NotifyAll();
                }
            }

            _outageTracker?.TrackDataSourceState(newState, newError);

            if (statusToBroadcast.HasValue)
            {
                _taskExecutor.ScheduleEvent(this, statusToBroadcast.Value, StatusChanged);
            }
        }

        internal async Task<bool> WaitForAsync(DataSourceState desiredState, TimeSpan timeout)
        {
            var deadline = DateTime.Now.Add(timeout);
            bool hasTimeout = timeout.CompareTo(TimeSpan.Zero) > 0;

            while (true)
            {
                MultiNotifierToken stateAwaiter;
                TimeSpan timeToWait;
                lock (_stateLock)
                {
                    if (_currentStatus.State == desiredState)
                    {
                        return true;
                    }
                    if (_currentStatus.State == DataSourceState.Off)
                    {
                        return false;
                    }

                    // Here we're using a slightly roundabout mechanism to keep track of however many tasks might
                    // be simultaneously waiting on WaitForAsync, because .NET doesn't have an async concurrency
                    // primitive equivalent to Java's wait/notifyAll(). What we're creating here is a cancellation
                    // token that will be cancelled (by UpdateStatus) the next time the status is changed in any
                    // way.
                    if (hasTimeout)
                    {
                        timeToWait = deadline.Subtract(DateTime.Now);
                        if (timeToWait.CompareTo(TimeSpan.Zero) <= 0)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        timeToWait = TimeSpan.FromMilliseconds(-1); // special value makes Task.Delay wait indefinitely
                    }
                    stateAwaiter = _stateChangedSignal.Token;
                }
                await stateAwaiter.WaitAsync(timeToWait);
            }
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                _stateChangedSignal.NotifyAll(); // in case anyone was waiting on this
            }
            _store.Dispose();
        }

        private void ReportStoreFailure(Exception e)
        {
            if (!_lastStoreUpdateFailed)
            {
                _log.Warn("Unexpected data store error when trying to store an update received from the data source: {0}",
                    LogValues.ExceptionSummary(e));
                _lastStoreUpdateFailed = true;
            }
            _log.Debug(LogValues.ExceptionTrace(e));
            UpdateStatus(DataSourceState.Interrupted, new DataSourceStatus.ErrorInfo
            {
                Kind = DataSourceStatus.ErrorKind.StoreError,
                Message = e.Message,
                Time = DateTime.Now
            });
        }

        private class OutageTracker
        {
            private readonly Logger _log;
            private readonly TimeSpan _loggingTimeout;
            private readonly object _trackerLock = new object();
            private readonly Dictionary<DataSourceStatus.ErrorInfo, int> _errorCounts =
                new Dictionary<DataSourceStatus.ErrorInfo, int>();

            private volatile bool _inOutage;
            private volatile TaskCompletionSource<bool> _outageEndedSignal;

            internal OutageTracker(Logger log, TimeSpan loggingTimeout)
            {
                _log = log;
                _loggingTimeout = loggingTimeout;
            }

            internal void TrackDataSourceState(DataSourceState newState, DataSourceStatus.ErrorInfo? newError)
            {
                lock (_trackerLock)
                {
                    if (newState == DataSourceState.Interrupted || newError.HasValue ||
                        (newState == DataSourceState.Initializing && _inOutage))
                    {
                        // We are in a potentially recoverable outage. If that wasn't the case already, and if
                        // we've been configured with a timeout for logging the outage at a higher level, schedule
                        // that timeout.
                        if (_inOutage)
                        {
                            // We were already in one - just record this latest error for logging later.
                            RecordError(newError);
                        }
                        else
                        {
                            // We weren't already in one, so set the timeout and start recording errors.
                            _inOutage = true;
                            _errorCounts.Clear();
                            RecordError(newError);
                            _outageEndedSignal = new TaskCompletionSource<bool>();
                            Task.Run(() => WaitForTimeout(_outageEndedSignal.Task));
                        }
                    }
                    else
                    {
                        if (_outageEndedSignal != null)
                        {
                            _outageEndedSignal.SetResult(true);
                            _outageEndedSignal = null;
                        }
                        _inOutage = false;
                    }
                }
            }

            private void RecordError(DataSourceStatus.ErrorInfo? newError)
            {
                if (!newError.HasValue)
                {
                    return;
                }
                // Accumulate how many times each kind of error has occurred during the outage - use just the basic
                // properties as the key so the map won't expand indefinitely
                var basicErrorInfo = new DataSourceStatus.ErrorInfo
                {
                    Kind = newError.Value.Kind,
                    StatusCode = newError.Value.StatusCode
                };
                if (_errorCounts.TryGetValue(basicErrorInfo, out var count))
                {
                    _errorCounts[basicErrorInfo] = count + 1;
                }
                else
                {
                    _errorCounts[basicErrorInfo] = 1;
                }
            }

            private async Task WaitForTimeout(Task outageEnded)
            {
                var timeoutTask = Task.Delay(_loggingTimeout);
                await Task.WhenAny(outageEnded, timeoutTask);
                lock (_trackerLock)
                {
                    _outageEndedSignal = null;
                    if (!_inOutage)
                    {
                        return;
                    }
                    var errorsDesc = string.Join(", ", _errorCounts.Select(kv => DescribeErrorCount(kv.Key, kv.Value)));
                    _log.Error("LaunchDarkly data source outage - updates have been unavailable for at least {0} with the following errors: {1}",
                        _loggingTimeout, errorsDesc);
                }
            }

            private string DescribeErrorCount(DataSourceStatus.ErrorInfo errorInfo, int count)
            {
                var errorDesc = errorInfo.StatusCode > 0 ?
                    string.Format("{0}({1})", errorInfo.Kind.Identifier(), errorInfo.StatusCode) :
                    errorInfo.Kind.Identifier();
                return string.Format("{0} ({1} {2})", errorDesc, count, count == 1 ? "time" : "times");
            }
        }
    }
}

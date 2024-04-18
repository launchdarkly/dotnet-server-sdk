using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal sealed class DataSourceOutageTracker
    {
        private readonly Logger _log;
        private readonly TimeSpan _loggingTimeout;
        private readonly object _trackerLock = new object();
        private readonly Dictionary<DataSourceStatus.ErrorInfo, int> _errorCounts =
            new Dictionary<DataSourceStatus.ErrorInfo, int>();

        private volatile bool _inOutage;
        private volatile TaskCompletionSource<bool> _outageEndedSignal;

        internal DataSourceOutageTracker(Logger log, TimeSpan loggingTimeout)
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

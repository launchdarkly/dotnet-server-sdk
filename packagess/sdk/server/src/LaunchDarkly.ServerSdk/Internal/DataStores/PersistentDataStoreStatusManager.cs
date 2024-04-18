using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Concurrent;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// Used internally to encapsulate the data store status broadcasting mechanism
    /// for PersistentDataStoreWrapper.
    /// </summary>
    /// <remarks>
    /// This is currently only used by PersistentDataStoreWrapper, but encapsulating it
    /// in its own class helps with clarity and also lets us reuse this logic in tests.
    /// </remarks>
    internal sealed class PersistentDataStoreStatusManager : IDisposable
    {
        internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500); // exposed for tests

        private readonly bool _refreshOnRecovery;
        private readonly Func<bool> _statusPollFn;
        private readonly Action<DataStoreStatus> _statusUpdater;
        private readonly TaskExecutor _taskExecutor;
        private readonly Logger _log;
        private readonly AtomicBoolean _lastAvailable;
        private readonly object _pollerLock = new object();

        private CancellationTokenSource _pollCanceller;

        internal PersistentDataStoreStatusManager(
            bool refreshOnRecovery,
            bool availableNow,
            Func<bool> statusPollFn,
            Action<DataStoreStatus> statusUpdater,
            TaskExecutor taskExecutor,
            Logger log
            )
        {
            _refreshOnRecovery = refreshOnRecovery;
            _lastAvailable = new AtomicBoolean(availableNow);
            _statusPollFn = statusPollFn;
            _statusUpdater = statusUpdater;
            _taskExecutor = taskExecutor;
            _log = log;
        }

        internal void UpdateAvailability(bool available)
        {
            if (_lastAvailable.GetAndSet(available) == available)
            {
                return; // no change
            }

            var status = new DataStoreStatus
            {
                Available = available,
                RefreshNeeded = available && _refreshOnRecovery
            };

            if (available)
            {
                _log.Warn("Persistent store is available again");
            }

            _statusUpdater(status);

            // If the store has just become unavailable, start a poller to detect when it comes back.
            // If it has become available, stop any polling we are currently doing.
            lock (_pollerLock)
            {
                if (available)
                {
                    _pollCanceller?.Cancel();
                    _pollCanceller = null;
                }
                else
                {
                    _log.Warn("Detected persistent store unavailability; updates will be cached until it recovers");

                    if (_pollCanceller is null)
                    {
                        // Start polling until the store starts working again
                        _pollCanceller = _taskExecutor.StartRepeatingTask(
                            PollInterval,
                            PollInterval,
                            () =>
                            {
                                if (_statusPollFn())
                                {
                                    UpdateAvailability(true);
                                }
                                return Task.FromResult(true); // return value doesn't matter here
                            }
                            );
                    }
                }
            }
        }

        public void Dispose() => _pollCanceller?.Cancel();
    }
}

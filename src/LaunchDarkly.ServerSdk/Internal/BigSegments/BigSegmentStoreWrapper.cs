using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Cache;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Concurrent;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;

namespace LaunchDarkly.Sdk.Server.Internal.BigSegments
{
    internal class BigSegmentStoreWrapper : IDisposable
    {
        private readonly IBigSegmentStore _store;
        private readonly TimeSpan _staleTime;
        private readonly ICache<string, IMembership> _cache;
        private readonly TaskExecutor _taskExecutor;
        private readonly CancellationTokenSource _pollCanceller;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly Logger _logger;

        private BigSegmentStoreStatus? _lastStatus;

        internal event EventHandler<BigSegmentStoreStatus> StatusChanged;

        internal BigSegmentStoreWrapper(
            BigSegmentsConfiguration config,
            TaskExecutor taskExecutor,
            Logger logger
            )
        {
            _store = config.Store;
            _staleTime = config.StaleAfter;
            _cache = Caches.KeyValue<string, IMembership>()
                .WithMaximumEntries(config.UserCacheSize)
                .WithExpiration(config.UserCacheTime)
                .WithLoader(QueryMembership)
                .Build();
            _taskExecutor = taskExecutor;
            _logger = logger;

            _pollCanceller = taskExecutor.StartRepeatingTask(
                TimeSpan.Zero,
                config.StatusPollInterval,
                PollStoreAndUpdateStatusAsync
                );
        }

        public void Dispose()
        {
            _pollCanceller.Cancel();
            _cache.Dispose();
            _store.Dispose();
        }

        /// <summary>
        /// Called by the evaluator when it needs to get the Big Segment membership state for
        /// a user.
        /// </summary>
        /// <remarks>
        /// If there is a cached membership state for the user, it returns the cached state. Otherwise,
        /// it converts the user key into the hash string used by the BigSegmentStore, queries the store,
        /// and caches the result. The returned status value indicates whether the query succeeded, and
        /// whether the result (regardless of whether it was from a new query or the cache) should be
        /// considered "stale".
        /// </remarks>
        /// <param name="userKey">the (unhashed) user key</param>
        /// <returns>the query result</returns>
        internal BigSegmentsQueryResult GetUserMembership(string userKey)
        {
            var ret = new BigSegmentsQueryResult();
            try
            {
                ret.Membership = _cache.Get(userKey); // loads value from store via QueryMembership if not already cached
                ret.Status = GetStatus().Stale ? BigSegmentsStatus.Stale : BigSegmentsStatus.Healthy;
            }
            catch (Exception e)
            {
                LogHelpers.LogException(_logger, "Big segment store returned error", e);
                ret.Membership = null;
                ret.Status = BigSegmentsStatus.StoreError;
            }
            return ret;
        }

        private IMembership QueryMembership(string userKey)
        {
            var hash = BigSegmentUserKeyHash(userKey);
            _logger.Debug("Querying Big Segment state for user hash {0}", hash);
            return AsyncUtils.WaitSafely(() => _store.GetMembershipAsync(hash));
        }

        /// <summary>
        /// Returns a BigSegmentStoreStatus describing whether the store seems to be available
        /// (that is, the last query to it did not return an error) and whether it is stale (that is, the last
        /// known update time is too far in the past).
        /// </summary>
        /// <remarks>
        /// If we have not yet obtained that information (the poll task has not executed yet), then this method
        /// immediately does a metadata query and waits for it to succeed or fail. This means that if an
        /// application using Big Segments evaluates a feature flag immediately after creating the SDK
        /// client, before the first status poll has happened, that evaluation may block for however long it
        /// takes to query the store.
        /// </remarks>
        /// <returns>the store status</returns>
        internal BigSegmentStoreStatus GetStatus()
        {
            BigSegmentStoreStatus? ret;
            _lock.EnterReadLock();
            try
            {
                ret = _lastStatus;
            }
            finally
            {
                _lock.ExitReadLock();
            }
            if (ret.HasValue)
            {
                return ret.Value;
            }
            return AsyncUtils.WaitSafely(() => PollStoreAndUpdateStatusAsync());
        }

        private async Task<BigSegmentStoreStatus> PollStoreAndUpdateStatusAsync()
        {
            var newStatus = new BigSegmentStoreStatus();
            _logger.Debug("Querying Big Segment store metadata");
            try
            {
                var metadata = await _store.GetMetadataAsync();
                newStatus.Available = true;
                newStatus.Stale = !metadata.HasValue ||
                    !metadata.Value.LastUpToDate.HasValue || IsStale(metadata.Value.LastUpToDate.Value);
            }
            catch (Exception e)
            {
                LogHelpers.LogException(_logger, "Big Segment store status query returned error", e);
                newStatus.Available = false;
            }

            BigSegmentStoreStatus? oldStatus = null;
            _lock.EnterWriteLock();
            try
            {
                oldStatus = _lastStatus;
                _lastStatus = newStatus;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            if (!oldStatus.HasValue || !newStatus.Equals(oldStatus.Value))
            {
                _logger.Debug("Big segment store status changed from {0} to {1}", oldStatus, newStatus);
                _taskExecutor.ScheduleEvent(newStatus, StatusChanged);
            }

            return newStatus;
        }

        private bool IsStale(UnixMillisecondTime updateTime) =>
            TimeSpan.FromMilliseconds(UnixMillisecondTime.Now.Value - updateTime.Value) >= _staleTime;
    }
}

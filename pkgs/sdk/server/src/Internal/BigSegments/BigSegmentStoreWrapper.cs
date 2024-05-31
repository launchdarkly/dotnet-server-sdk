using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchDarkly.Cache;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Concurrent;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Subsystems;

using static LaunchDarkly.Sdk.Server.Subsystems.BigSegmentStoreTypes;
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
        private readonly Task<BigSegmentStoreStatus> _initialPoll;

        private BigSegmentStoreStatus? _lastStatus;
        private int count = 0;

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
                .WithMaximumEntries(config.ContextCacheSize)
                .WithExpiration(config.ContextCacheTime)
                .WithLoader(QueryMembership)
                .Build();
            _taskExecutor = taskExecutor;
            _logger = logger;

            _initialPoll = Task.Run(PollStoreAndUpdateStatusAsync);
            _pollCanceller = taskExecutor.StartRepeatingTask(
                config.StatusPollInterval,
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
        /// a context.
        /// </summary>
        /// <remarks>
        /// If there is a cached membership state for the context, it returns the cached state. Otherwise,
        /// it converts the user key into the hash string used by the BigSegmentStore, queries the store,
        /// and caches the result. The returned status value indicates whether the query succeeded, and
        /// whether the result (regardless of whether it was from a new query or the cache) should be
        /// considered "stale".
        /// </remarks>
        /// <param name="contextKey">the (unhashed) context key</param>
        /// <returns>the query result</returns>
        internal BigSegmentsQueryResult GetMembership(string contextKey)
        {
            var ret = new BigSegmentsQueryResult();
            try
            {
                ret.Membership = _cache.Get(contextKey); // loads value from store via QueryMembership if not already cached
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

        private IMembership QueryMembership(string contextKey)
        {
            var hash = BigSegmentContextKeyHash(contextKey);
            _logger.Debug("Querying Big Segment state for context hash {0}", hash);
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
            return ret ?? _initialPoll.GetAwaiter().GetResult();
        }

        private async Task<BigSegmentStoreStatus> PollStoreAndUpdateStatusAsync()
        {
            count++;
            if (count == 2)
            {
                Console.WriteLine("POTATO");
            }
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

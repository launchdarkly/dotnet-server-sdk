using System;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the SDK's Big Segments behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Big Segments" are a specific type of user segments. For more information, read the LaunchDarkly
    /// documentation about user segments: https://docs.launchdarkly.com/home/users/segments
    /// </para>
    /// <para>
    /// If you want to set non-default values for any of these properties, create a builder with
    /// <see cref="Components.BigSegments(IBigSegmentStoreFactory)"/>, change its properties with the
    /// methods of this class, and pass it to <see cref="ConfigurationBuilder.BigSegments(IBigSegmentsConfigurationFactory)"/>:
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    ///     // This example uses the Redis integration
    ///     var config = Configuration.Builder(sdkKey)
    ///         .BigSegments(Components.BigSegments(Redis.DataStore().Prefix("app1"))
    ///             .UserCacheSize(2000))
    ///         .Build();
    /// </code>
    /// </example>
    public sealed class BigSegmentsConfigurationBuilder : IBigSegmentsConfigurationFactory
    {
        /// <summary>
        /// Default value for <see cref="UserCacheSize(int)"/>.
        /// </summary>
        public const int DefaultUserCacheSize = 1000;

        /// <summary>
        /// Default value for <see cref="UserCacheTime(TimeSpan)"/>: five seconds.
        /// </summary>
        public static readonly TimeSpan DefaultUserCacheTime = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Default value for <see cref="StatusPollInterval(TimeSpan)"/>: five seconds.
        /// </summary>
        public static readonly TimeSpan DefaultStatusPollInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Default value for <see cref="StaleAfter(TimeSpan)"/>: two minutes.
        /// </summary>
        public static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromMinutes(2);

        private readonly IBigSegmentStoreFactory _storeFactory;
        private int _userCacheSize = DefaultUserCacheSize;
        private TimeSpan _userCacheTime = DefaultUserCacheTime;
        private TimeSpan _statusPollInterval = DefaultStatusPollInterval;
        private TimeSpan _staleAfter = DefaultStaleAfter;

        internal BigSegmentsConfigurationBuilder(IBigSegmentStoreFactory storeFactory)
        {
            _storeFactory = storeFactory;
        }

        /// <summary>
        /// Sets the maximum number of users whose Big Segment state will be cached by the SDK
        /// at any given time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// To reduce database traffic, the SDK maintains a least-recently-used cache by user key. When a feature
        /// flag that references a Big Segment is evaluated for some user who is not currently in the cache, the
        /// SDK queries the database for all Big Segment memberships of that user, and stores them together in a
        /// single cache entry. If the cache is full, the oldest entry is dropped.
        /// </para>
        /// <para>
        /// A higher value for <see cref="UserCacheSize(int)"/> means that database queries for Big Segments will
        /// be done less often for recently-referenced users, if the application has many users, at the cost of
        /// increased memory used by the cache.
        /// </para>
        /// <para>
        /// Cache entries can also expire based on the setting of <see cref="UserCacheTime(TimeSpan)"/>.
        /// </para>
        /// </remarks>
        /// <param name="userCacheSize">the maximum number of user states to cache</param>
        /// <returns>the builder</returns>
        /// <seealso cref="DefaultUserCacheSize"/>
        public BigSegmentsConfigurationBuilder UserCacheSize(int userCacheSize)
        {
            _userCacheSize = userCacheSize;
            return this;
        }

        /// <summary>
        /// Sets the maximum length of time that the Big Segment state for a user will be cached
        /// by the SDK.
        /// </summary>
        /// <remarks>
        /// <para>
        /// See <see cref="UserCacheSize(int)"/> for more about this cache. A higher value for
        /// <see cref="UserCacheTime(TimeSpan)"/> means that database queries for the Big Segment state of any
        /// given user will be done less often, but that changes to segment membership may not be detected as soon.
        /// </para>
        /// </remarks>
        /// <param name="userCacheTime">the cache TTL</param>
        /// <returns>the builder</returns>
        /// <seealso cref="DefaultUserCacheTime"/>
        public BigSegmentsConfigurationBuilder UserCacheTime(TimeSpan userCacheTime)
        {
            _userCacheTime = userCacheTime;
            return this;
        }

        /// <summary>
        /// Sets the interval at which the SDK will poll the Big Segment store to make sure
        /// it is available and to determine how long ago it was updated.
        /// </summary>
        /// <param name="statusPollInterval">the status polling interval (any value less than or
        /// equal to zero will be changed to <see cref="DefaultStatusPollInterval"/>)</param>
        /// <returns>the builder</returns>
        /// <seealso cref="DefaultStatusPollInterval"/>
        public BigSegmentsConfigurationBuilder StatusPollInterval(TimeSpan statusPollInterval)
        {
            _statusPollInterval = statusPollInterval > TimeSpan.Zero ? statusPollInterval :
                DefaultStatusPollInterval;
            return this;
        }

        /// <summary>
        /// Sets the maximum length of time between updates of the Big Segments data before the data
        /// is considered out of date.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Normally, the LaunchDarkly Relay Proxy updates a timestamp in the Big Segments store at intervals to
        /// confirm that it is still in sync with the LaunchDarkly data, even if there have been no changes to the
        /// data. If the timestamp falls behind the current time by the amount specified in
        /// <see cref="StaleAfter(TimeSpan)"/>, the SDK assumes that something is not working correctly in this
        /// process and that the data may not be accurate.
        /// </para>
        /// <para>
        /// While in a stale state, the SDK will still continue using the last known data, but
        /// <see cref="IBigSegmentStoreStatusProvider.Status"/> will return true in its Stale property, and any
        /// <see cref="EvaluationReason"/> generated from a feature flag that references a Big Segment will have
        /// a <see cref="EvaluationReason.BigSegmentsStatus"/> of <see cref="BigSegmentsStatus.Stale"/>.
        /// </para>
        /// </remarks>
        /// <param name="staleAfter">the time limit for marking the data as stale (any value less
        /// than or equal to zero will be changed to <see cref="DefaultStaleAfter"/>)</param>
        /// <returns>the builder</returns>
        public BigSegmentsConfigurationBuilder StaleAfter(TimeSpan staleAfter)
        {
            _staleAfter = staleAfter > TimeSpan.Zero ? staleAfter : DefaultStaleAfter;
            return this;
        }

        /// <inheritdoc/>
        public BigSegmentsConfiguration CreateBigSegmentsConfiguration(LdClientContext context)
        {
            var store = _storeFactory is null ? null : _storeFactory.CreateBigSegmentStore(context);
            return new BigSegmentsConfiguration(
                store,
                _userCacheSize,
                _userCacheTime,
                _statusPollInterval,
                _staleAfter
                );
        }
    }
}

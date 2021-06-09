using System;
using System.Threading;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// A configurable data store factory that adds caching behavior to a persistent data
    /// store implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a persistent data store (e.g. a database integration), the store implementation will
    /// provide an <see cref="IPersistentDataStoreFactory"/> or <see cref="IPersistentDataStoreAsyncFactory"/>
    /// that implements the specific data store behavior. The SDK then provides additional
    /// options for caching; those are defined by this type, which is returned by
    /// <see cref="Components.PersistentDataStore(IPersistentDataStoreFactory)"/>. Example usage:
    /// </para>
    /// <code>
    ///     var myStore = Components.PersistentDataStore(Redis.FeatureStore())
    ///         .CacheTtl(TimeSpan.FromSeconds(45));
    ///     var config = Configuration.Builder(sdkKey)
    ///         .DataStore(myStore)
    ///         .Build();
    /// </code>
    /// </remarks>
    public class PersistentDataStoreBuilder : IDataStoreFactory, IDiagnosticDescription
    {
        private readonly IPersistentDataStoreFactory _coreFactory;
        private readonly IPersistentDataStoreAsyncFactory _coreAsyncFactory;
        private DataStoreCacheConfig _cacheConfig = DataStoreCacheConfig.Enabled;

        /// <summary>
        /// The default cache expiration time.
        /// </summary>
        public static readonly TimeSpan DefaultTtl = DataStoreCacheConfig.DefaultTtl;

        internal PersistentDataStoreBuilder(IPersistentDataStoreFactory coreFactory)
        {
            _coreFactory = coreFactory;
            _coreAsyncFactory = null;
        }

        internal PersistentDataStoreBuilder(IPersistentDataStoreAsyncFactory coreAsyncFactory)
        {
            _coreFactory = null;
            _coreAsyncFactory = coreAsyncFactory;
        }

        /// <summary>
        /// Specifies that the SDK should <i>not</i> use an in-memory cache for the persistent data store.
        /// </summary>
        /// <remarks>
        /// This means that every feature flag evaluation will trigger a data store query.
        /// </remarks>
        /// <returns>the builder</returns>
        public PersistentDataStoreBuilder NoCaching() => CacheTime(TimeSpan.Zero);

        /// <summary>
        /// Specifies the cache TTL. Items will expire from the cache after this amount of time from the
        /// time when they were originally cached.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the value is <c>TimeSpan.Zero</c>, caching is disabled (equivalent to <see cref="NoCaching"/>).
        /// </para>
        /// <para>
        /// If the value is <c>System.Threading.Timeout.InfiniteTimeSpan</c> (or any negative number), data is
        /// cached forever (equivalent to <see cref="CacheForever"/>).
        /// </para>
        /// </remarks>
        /// <param name="cacheTime">the cache TTL</param>
        /// <returns>the builder</returns>
        public PersistentDataStoreBuilder CacheTime(TimeSpan cacheTime)
        {
            _cacheConfig = _cacheConfig.WithTtl(cacheTime);
            return this;
        }

        /// <summary>
        /// Shortcut for calling <see cref="CacheTime(TimeSpan)"/> with a time span in milliseconds.
        /// </summary>
        /// <param name="millis">the cache TTL in milliseconds</param>
        /// <returns>the builder</returns>
        public PersistentDataStoreBuilder CacheMillis(int millis) =>
            CacheTime(TimeSpan.FromMilliseconds(millis));

        /// <summary>
        /// Shortcut for calling <see cref="CacheTime(TimeSpan)"/> with a time span in seconds.
        /// </summary>
        /// <param name="seconds">the cache TTL in seconds</param>
        /// <returns>the builder</returns>
        public PersistentDataStoreBuilder CacheSeconds(int seconds) =>
            CacheTime(TimeSpan.FromSeconds(seconds));

        /// <summary>
        /// Specifies the maximum number of entries that can be held in the cache at a time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If this limit is exceeded, older entries will be evicted from the cache to make room
        /// for new ones.
        /// </para>
        /// <para>
        /// If this is null, there is no limit on the number of entries.
        /// </para>
        /// </remarks>
        /// <param name="maximumEntries">the maximum number of entries, or null for no limit</param>
        /// <returns>an updated factory object</returns>
        public PersistentDataStoreBuilder CacheMaximumEntries(int? maximumEntries)
        {
            _cacheConfig = _cacheConfig.WithMaximumEntries(maximumEntries);
            return this;
        }

        /// <summary>
        /// Specifies that the in-memory cache should never expire.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In this mode, data will be written to both the underlying persistent store and the cache,
        /// but will only ever be read <i>from</i> the persistent store if the SDK is restarted.
        /// </para>
        /// <para>
        /// Use this mode with caution: it means that in a scenario where multiple processes are sharing
        /// the database, and the current process loses connectivity to LaunchDarkly while other processes
        /// are still receiving updates and writing them to the database, the current process will have
        /// stale data.
        /// </para>
        /// </remarks>
        /// <returns>the builder</returns>
        public PersistentDataStoreBuilder CacheForever() => CacheTime(Timeout.InfiniteTimeSpan);

        /// <inheritdoc/>
        public IDataStore CreateDataStore(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
        {
            if (_coreFactory != null)
            {
                return new PersistentStoreWrapper(
                    _coreFactory.CreatePersistentDataStore(context),
                    _cacheConfig,
                    dataStoreUpdates,
                    context.TaskExecutor,
                    context.Basic.Logger
                    );
            }
            else if (_coreAsyncFactory != null)
            {
                return new PersistentStoreWrapper(
                    _coreAsyncFactory.CreatePersistentDataStore(context),
                    _cacheConfig,
                    dataStoreUpdates,
                    context.TaskExecutor,
                    context.Basic.Logger
                    );
            }
            return null;
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic)
        {
            if (_coreFactory != null && _coreFactory is IDiagnosticDescription dd1)
            {
                return dd1.DescribeConfiguration(basic);
            }
            if (_coreAsyncFactory != null && _coreAsyncFactory is IDiagnosticDescription dd2)
            {
                return dd2.DescribeConfiguration(basic);
            }
            return LdValue.Of("custom");
        }
    }
}

using System;
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
    /// <see cref="Components.PersistentStore(IPersistentDataStoreFactory)"/>. Example usage:
    /// </para>
    /// <code>
    ///     var myStore = Components.PersistentStore(Redis.FeatureStore())
    ///         .CacheTtl(TimeSpan.FromSeconds(45));
    ///     var config = Configuration.Builder(sdkKey)
    ///         .DataStore(myStore)
    ///         .Build();
    /// </code>
    /// </remarks>
    public class PersistentDataStoreConfiguration : IDataStoreFactory
    {
        private readonly IPersistentDataStoreFactory _coreFactory;
        private readonly IPersistentDataStoreAsyncFactory _coreAsyncFactory;
        private DataStoreCacheConfig _cacheConfig = DataStoreCacheConfig.Enabled;

        /// <summary>
        /// The default cache expiration time.
        /// </summary>
        public static readonly TimeSpan DefaultTtl = DataStoreCacheConfig.DefaultTtl;

        internal PersistentDataStoreConfiguration(IPersistentDataStoreFactory coreFactory)
        {
            _coreFactory = coreFactory;
            _coreAsyncFactory = null;
        }

        internal PersistentDataStoreConfiguration(IPersistentDataStoreAsyncFactory coreAsyncFactory)
        {
            _coreFactory = null;
            _coreAsyncFactory = coreAsyncFactory;
        }

        /// <summary>
        /// Specifies the cache TTL. Items will expire from the cache after this amount of time from the
        /// time when they were originally cached.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Specifying <c>TimeSpan.Zero</c> disables caching, so every feature flag request will cause a query
        /// to the data store.
        /// </para>
        /// <para>
        /// Specifying <c>System.Threading.Timeout.InfiniteTimeSpan</c> (or any negative number) turns off cache
        /// expiration, so the data store will only be queried the first time a particular item is used; all updates
        /// received from LaunchDarkly will be written to both the data store and the cache. Use this "cached forever"
        /// mode with caution: it means that in a scenario where multiple processes are sharing the database, and the
        /// current process loses connectivity to LaunchDarkly while other processes are still receiving updates and
        /// writing them to the database, the current process will have stale data.
        /// </para>
        /// </remarks>
        /// <param name="ttl">the cache TTL</param>
        /// <returns>an updated factory object</returns>
        public PersistentDataStoreConfiguration CacheTtl(TimeSpan ttl)
        {
            _cacheConfig = _cacheConfig.WithTtl(ttl);
            return this;
        }

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
        public PersistentDataStoreConfiguration CacheMaximumEntries(int? maximumEntries)
        {
            _cacheConfig = _cacheConfig.WithMaximumEntries(maximumEntries);
            return this;
        }

        /// <summary>
        /// Called by the SDK to create the data store instance for a specific <see cref="LdClient"/>.
        /// </summary>
        /// <param name="context">used internally</param>
        /// <returns>the data store instance</returns>
        public IDataStore CreateDataStore(LdClientContext context)
        {
            if (_coreFactory != null)
            {
                return new PersistentStoreWrapper(_coreFactory.CreatePersistentDataStore(), _cacheConfig);
            }
            else if (_coreAsyncFactory != null)
            {
                return new PersistentStoreWrapper(_coreAsyncFactory.CreatePersistentDataStore(), _cacheConfig);
            }
            return null;
        }
    }
}

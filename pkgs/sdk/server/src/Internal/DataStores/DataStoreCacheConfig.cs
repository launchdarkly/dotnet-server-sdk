using System;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// Internal abstraction of caching parameters used by <see cref="PersistentStoreWrapper"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Application code cannot see this class and instead uses the configuration methods on
    /// <see cref="LaunchDarkly.Sdk.Server.Integrations.PersistentDataStoreBuilder"/>.
    /// </para>
    /// </remarks>
    internal sealed class DataStoreCacheConfig
    {
        /// <summary>
        /// The default cache expiration time.
        /// </summary>
        public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The cache expiration time. Caching is enabled if this is greater than zero.
        /// </summary>
        /// <remarks>
        /// If the value is negative (such as <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>), data is cached
        /// forever (i.e. it will only be read again from the database if the SDK is restarted).
        /// </remarks>
        /// <seealso cref="WithTtl(TimeSpan)"/>
        /// <seealso cref="WithTtlMillis(double)"/>
        /// <seealso cref="WithTtlSeconds(double)"/>
        public TimeSpan Ttl { get; private set; }

        /// <summary>
        /// True if caching is enabled.
        /// </summary>
        public bool IsEnabled => Ttl != TimeSpan.Zero;

        /// <summary>
        /// True if caching is enabled and does not have a finite TTL.
        /// </summary>
        public bool IsInfiniteTtl => Ttl < TimeSpan.Zero;

        /// <summary>
        /// The maximum number of entries that can be held in the cache at a time.
        /// </summary>
        public int? MaximumEntries { get; }

        /// <summary>
        /// Returns a parameter object indicating that caching should be disabled.
        /// </summary>
        public static readonly DataStoreCacheConfig Disabled = new DataStoreCacheConfig(TimeSpan.Zero, null);

        /// <summary>
        /// Returns a parameter object indicating that caching should be enabled, using the
        /// default TTL of <see cref="DefaultTtl"/>.
        /// </summary>
        public static readonly DataStoreCacheConfig Enabled = new DataStoreCacheConfig(DefaultTtl, null);

        internal DataStoreCacheConfig(TimeSpan ttl, int? maximumEntries)
        {
            Ttl = ttl;
            MaximumEntries = maximumEntries;
        }
        
        /// <summary>
        /// Specifies the cache TTL. Items will expire from the cache after this amount of time from the
        /// time when they were originally cached.
        /// </summary>
        /// <param name="ttl">the cache TTL; must be greater than zero</param>
        /// <returns>an updated parameters object</returns>
        public DataStoreCacheConfig WithTtl(TimeSpan ttl)
        {
            return new DataStoreCacheConfig(ttl, MaximumEntries);
        }

        /// <summary>
        /// Shortcut for calling <see cref="WithTtl"/> with a TimeSpan in milliseconds.
        /// </summary>
        /// <param name="millis">the cache TTL in milliseconds</param>
        /// <returns>an updated parameters object</returns>
        public DataStoreCacheConfig WithTtlMillis(double millis)
        {
            return WithTtl(TimeSpan.FromMilliseconds(millis));
        }

        /// <summary>
        /// Shortcut for calling <see cref="WithTtl"/> with a TimeSpan in seconds.
        /// </summary>
        /// <param name="seconds">the cache TTL in seconds</param>
        /// <returns>an updated parameters object</returns>
        public DataStoreCacheConfig WithTtlSeconds(double seconds)
        {
            return WithTtl(TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Specifies the maximum number of entries that can be held in the cache at a time.
        /// If this limit is exceeded, older entries will be evicted from the cache to make room
        /// for new ones.
        /// 
        /// If this is null, there is no limit on the number of entries.
        /// </summary>
        /// <param name="maximumEntries">the maximum number of entries, or null for no limit</param>
        /// <returns>an updated parameters object</returns>
        public DataStoreCacheConfig WithMaximumEntries(int? maximumEntries)
        {
            if (maximumEntries != null && maximumEntries <= 0)
            {
                throw new ArgumentException("must be > 0 if not null", nameof(maximumEntries));
            }
            return new DataStoreCacheConfig(Ttl, maximumEntries);
        }
    }
}

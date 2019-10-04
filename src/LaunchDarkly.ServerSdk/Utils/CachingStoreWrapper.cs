using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Cache;

namespace LaunchDarkly.Client.Utils
{
    /// <summary>
    /// A partial implementation of <see cref="IFeatureStore"/> that delegates the basic functionality to
    /// an instance of <see cref="IFeatureStoreCore"/> or <see cref="IFeatureStoreCoreAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides optional caching behavior and other logic that would otherwise be repeated in
    /// every feature store implementation. This makes it easier to create new database integrations by
    /// implementing only the database-specific logic.
    /// </para>
    /// <para>
    /// Construct instances of this class with <see cref="CachingStoreWrapper.Builder(IFeatureStoreCore)"/>
    /// or <see cref="CachingStoreWrapper.Builder(IFeatureStoreCoreAsync)"/>.
    /// </para>
    /// </remarks>
    public sealed class CachingStoreWrapper : IFeatureStore
    {
        private readonly IFeatureStoreCore _core;
        private readonly FeatureStoreCacheConfig _caching;
        
        private readonly ICache<CacheKey, IVersionedData> _itemCache;
        private readonly ICache<IVersionedDataKind, ImmutableDictionary<string, IVersionedData>> _allCache;
        private readonly ISingleValueCache<bool> _initCache;
        private volatile bool _inited;

        /// <summary>
        /// Creates a new builder using a synchronous data store implementation.
        /// </summary>
        /// <param name="core">the <see cref="IFeatureStoreCore"/> implementation</param>
        /// <returns>a builder</returns>
        public static CachingStoreWrapperBuilder Builder(IFeatureStoreCore core)
        {
            return new CachingStoreWrapperBuilder(core);
        }

        /// <summary>
        /// Creates a new builder using an asynchronous data store implementation.
        /// </summary>
        /// <param name="coreAsync">the <see cref="IFeatureStoreCoreAsync"/> implementation</param>
        /// <returns>a builder</returns>
        public static CachingStoreWrapperBuilder Builder(IFeatureStoreCoreAsync coreAsync)
        {
            return new CachingStoreWrapperBuilder(new FeatureStoreCoreAsyncAdapter(coreAsync));
        }

        internal CachingStoreWrapper(IFeatureStoreCore core, FeatureStoreCacheConfig caching)
        {
            this._core = core;
            this._caching = caching;

            if (caching.IsEnabled)
            {
                var itemCacheBuilder = Caches.KeyValue<CacheKey, IVersionedData>()
                    .WithLoader(GetInternalForCache)
                    .WithMaximumEntries(caching.MaximumEntries);
                var allCacheBuilder = Caches.KeyValue<IVersionedDataKind, ImmutableDictionary<string, IVersionedData>>()
                    .WithLoader(GetAllForCache);
                var initCacheBuilder = Caches.SingleValue<bool>()
                    .WithLoader(_core.InitializedInternal);
                if (!caching.IsInfiniteTtl)
                {
                    itemCacheBuilder.WithExpiration(caching.Ttl);
                    allCacheBuilder.WithExpiration(caching.Ttl);
                    initCacheBuilder.WithExpiration(caching.Ttl);
                }
                _itemCache = itemCacheBuilder.Build();
                _allCache = allCacheBuilder.Build();
                _initCache = initCacheBuilder.Build();
            }
            else
            {
                _itemCache = null;
                _allCache = null;
                _initCache = null;
            }
        }

        /// <inheritdoc/>
        public bool Initialized()
        {
            if (_inited)
            {
                return true;
            }
            bool result;
            if (_initCache != null)
            {
                result = _initCache.Get();
            }
            else
            {
                result = _core.InitializedInternal();
            }
            if (result)
            {
                _inited = true;
            }
            return result;
        }

        /// <inheritdoc/>
        public void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> items)
        {
            Exception failure = null;
            try
            {
                _core.InitInternal(items);
            }
            catch (Exception e)
            {
                failure = e;
            }
            if (_itemCache != null && _allCache != null)
            {
                _itemCache.Clear();
                _allCache.Clear();
                if (failure != null && !_caching.IsInfiniteTtl)
                {
                    // Normally, if the underlying store failed to do the update, we do not want to update the cache -
                    // the idea being that it's better to stay in a consistent state of having old data than to act
                    // like we have new data but then suddenly fall back to old data when the cache expires. However,
                    // if the cache TTL is infinite, then it makes sense to update the cache always.
                    throw failure;
                }
                foreach (var e0 in items)
                {
                    var kind = e0.Key;
                    _allCache.Set(kind, e0.Value.ToImmutableDictionary());
                    foreach (var e1 in e0.Value)
                    {
                        _itemCache.Set(new CacheKey(kind, e1.Key), e1.Value);
                    }
                }
            }
            if (failure is null || _caching.IsInfiniteTtl)
            {
                _inited = true;
            }
            if (failure != null)
            {
                throw failure;
            }
        }

        /// <inheritdoc/>
        public T Get<T>(VersionedDataKind<T> kind, String key) where T : class, IVersionedData
        {
            T item;
            if (_itemCache != null)
            {
                item = (T)_itemCache.Get(new CacheKey(kind, key));
            }
            else
            {
                item = (T)_core.GetInternal(kind, key);
            }
            return (item == null || item.Deleted) ? null : item;
        }

        /// <inheritdoc/>
        public IDictionary<string, T> All<T>(VersionedDataKind<T> kind) where T : class, IVersionedData
        {
            if (_allCache != null)
            {
                return FilterItems<T>(_allCache.Get(kind));
            }
            return FilterItems<T>(_core.GetAllInternal(kind));
        }

        /// <inheritdoc/>
        public void Upsert<T>(VersionedDataKind<T> kind, T item) where T : IVersionedData
        {
            Exception failure = null;
            IVersionedData newState = item;
            try
            {
                newState = _core.UpsertInternal(kind, item);
            }
            catch (Exception e)
            {
                // Normally, if the underlying store failed to do the update, we do not want to update the cache -
                // the idea being that it's better to stay in a consistent state of having old data than to act
                // like we have new data but then suddenly fall back to old data when the cache expires. However,
                // if the cache TTL is infinite, then it makes sense to update the cache always.
                if (!_caching.IsInfiniteTtl)
                {
                    throw;
                }
                failure = e;
            }
            if (_itemCache != null)
            {
                _itemCache.Set(new CacheKey(kind, item.Key), newState);
            }
            if (_allCache != null)
            {
                // If the cache has a finite TTL, then we should remove the "all items" cache entry to force
                // a reread the next time All is called. However, if it's an infinite TTL, we need to just
                // update the item within the existing "all items" entry (since we want things to still work
                // even if the underlying store is unavailable).
                if (_caching.IsInfiniteTtl)
                {
                    try
                    {
                        var cachedAll = _allCache.Get(kind);
                        _allCache.Set(kind, cachedAll.SetItem(item.Key, newState));
                    }
                    catch (Exception) { }
                    // An exception here means that we did not have a cached value for All, so it tried to query
                    // the underlying store, which failed (not surprisingly since it just failed a moment ago
                    // when we tried to do an update). This should not happen in infinite-cache mode, but if it
                    // does happen, there isn't really anything we can do.
                }
                else
                {
                    _allCache.Remove(kind);
                }
            }
            if (failure != null)
            {
                throw failure;
            }
        }

        /// <inheritdoc/>
        public void Delete<T>(VersionedDataKind<T> kind, string key, int version) where T : IVersionedData
        {
            Upsert(kind, kind.MakeDeletedItem(key, version));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _core.Dispose();
                _itemCache?.Dispose();
                _allCache?.Dispose();
                _initCache?.Dispose();
            }
        }
        
        private IVersionedData GetInternalForCache(CacheKey key)
        {
            return _core.GetInternal(key.Kind, key.Key);
        }

        private ImmutableDictionary<string, IVersionedData> GetAllForCache(IVersionedDataKind kind)
        {
            return _core.GetAllInternal(kind).ToImmutableDictionary();
        }

        private IDictionary<string, T> FilterItems<T>(IDictionary<string, IVersionedData> items) where T : IVersionedData
        {
            return items.Where(kv => !kv.Value.Deleted).ToDictionary(i => i.Key, i => (T)i.Value);
        }
    }

    /// <summary>
    /// Builder class for <see cref="CachingStoreWrapper"/>.
    /// </summary>
    public class CachingStoreWrapperBuilder
    {
        private readonly IFeatureStoreCore _core;
        private FeatureStoreCacheConfig _caching = FeatureStoreCacheConfig.Enabled;

        internal CachingStoreWrapperBuilder(IFeatureStoreCore core)
        {
            _core = core;
        }

        /// <summary>
        /// Creates and configures the wrapper object.
        /// </summary>
        /// <returns>a <see cref="CachingStoreWrapper"/> instance</returns>
        public CachingStoreWrapper Build()
        {
            return new CachingStoreWrapper(_core, _caching);
        }

        /// <summary>
        /// Sets the local caching properties.
        /// </summary>
        /// <param name="caching">a <see cref="FeatureStoreCacheConfig"/> object</param>
        /// <returns>the builder</returns>
        public CachingStoreWrapperBuilder WithCaching(FeatureStoreCacheConfig caching)
        {
            _caching = caching;
            return this;
        }
    }

    internal struct CacheKey : IEquatable<CacheKey>
    {
        public readonly IVersionedDataKind Kind;
        public readonly string Key;

        public CacheKey(IVersionedDataKind kind, string key)
        {
            Kind = kind;
            Key = key;
        }

        public bool Equals(CacheKey other)
        {
            return Kind == other.Kind && Key == other.Key;
        }

        public override int GetHashCode()
        {
            return Kind.GetHashCode() * 17 + Key.GetHashCode();
        }
    }
}

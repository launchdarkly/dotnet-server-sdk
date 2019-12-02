using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Cache;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// The SDK's internal implementation <see cref="IDataStore"/> for persistent data stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The basic data store behavior is provided by some implementation of
    /// <see cref="IPersistentDataStore"/> or <see cref="IPersistentDataStoreAsync"/>. This
    /// class adds the caching behavior that we normally want for any persistent data store.
    /// </para>
    /// </remarks>
    internal sealed class PersistentStoreWrapper : IDataStore
    {
        private readonly IPersistentDataStore _core;
        private readonly DataStoreCacheConfig _caching;
        
        private readonly ICache<CacheKey, ItemDescriptor?> _itemCache;
        private readonly ICache<DataKind, ImmutableDictionary<string, ItemDescriptor>> _allCache;
        private readonly ISingleValueCache<bool> _initCache;
        private volatile bool _inited;
        
        internal PersistentStoreWrapper(IPersistentDataStoreAsync coreAsync, DataStoreCacheConfig caching) :
            this(new PersistentStoreAsyncAdapter(coreAsync), caching)
        { }

        internal PersistentStoreWrapper(IPersistentDataStore core, DataStoreCacheConfig caching)
        {
            this._core = core;
            this._caching = caching;

            if (caching.IsEnabled)
            {
                var itemCacheBuilder = Caches.KeyValue<CacheKey, ItemDescriptor?>()
                    .WithLoader(GetInternalForCache)
                    .WithMaximumEntries(caching.MaximumEntries);
                var allCacheBuilder = Caches.KeyValue<DataKind, ImmutableDictionary<string, ItemDescriptor>>()
                    .WithLoader(GetAllAndDeserialize);
                var initCacheBuilder = Caches.SingleValue<bool>()
                    .WithLoader(_core.Initialized);
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
                result = _core.Initialized();
            }
            if (result)
            {
                _inited = true;
            }
            return result;
        }

        public void Init(FullDataSet<ItemDescriptor> items)
        {
            Exception failure = null;
            var serializedItems = items.Data.ToImmutableDictionary(
                kindAndItems => kindAndItems.Key,
                kindAndItems => (IEnumerable<KeyValuePair<string, SerializedItemDescriptor>>)
                    kindAndItems.Value.ToImmutableDictionary(
                        keyAndItem => keyAndItem.Key,
                        keyAndItem => Serialize(kindAndItems.Key, keyAndItem.Value)
                    )
            );
            try
            {
                _core.Init(new FullDataSet<SerializedItemDescriptor>(serializedItems));
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
                foreach (var e0 in items.Data)
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
        
        public ItemDescriptor? Get(DataKind kind, string key) =>
            _itemCache is null ? GetAndDeserializeItem(kind, key) :
                _itemCache.Get(new CacheKey(kind, key));

        public IEnumerable<KeyValuePair<string, ItemDescriptor>> GetAll(DataKind kind) =>
            _allCache is null ? GetAllAndDeserialize(kind) : _allCache.Get(kind);

        public bool Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            var serializedItem = new SerializedItemDescriptor(item.Version,
                item.Item is null ? null : kind.Serialize(item.Item));
            bool updated = false;
            Exception failure = null;
            try
            {
                updated = _core.Upsert(kind, key, serializedItem);
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
                var cacheKey = new CacheKey(kind, key);
                if (failure is null)
                {
                    if (updated)
                    {
                        _itemCache.Set(cacheKey, item);
                    }
                    else
                    {
                        // there was a concurrent modification elsewhere - update the cache to get the new state
                        _itemCache.Remove(cacheKey);
                        _itemCache.Get(cacheKey);
                    }
                }
                else
                {
                    try
                    {
                        var oldItem = _itemCache.Get(cacheKey);
                        if (!oldItem.HasValue || oldItem.Value.Version < item.Version)
                        {
                            _itemCache.Set(cacheKey, item);
                        }
                    }
                    catch (Exception)
                    {
                        // An exception here means that the underlying database is down *and* there was no
                        // cached item; in that case we just go ahead and update the cache.
                        _itemCache.Set(cacheKey, item);
                    }
                }
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
                        _allCache.Set(kind, cachedAll.SetItem(key, item));
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
            return updated;
        }
        
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

        private ItemDescriptor? GetInternalForCache(CacheKey key) =>
            GetAndDeserializeItem(key.Kind, key.Key);

        private ItemDescriptor? GetAndDeserializeItem(DataKind kind, string key)
        {
            var maybeSerializedItem = _core.Get(kind, key);
            if (!maybeSerializedItem.HasValue)
            {
                return null;
            }
            return Deserialize(kind, maybeSerializedItem.Value);
        }
        
        private ImmutableDictionary<string, ItemDescriptor> GetAllAndDeserialize(DataKind kind)
        {
            return _core.GetAll(kind).ToImmutableDictionary(
                kv => kv.Key,
                kv => Deserialize(kind, kv.Value));

        }

        private SerializedItemDescriptor Serialize(DataKind kind, ItemDescriptor itemDesc)
        {
            var item = itemDesc.Item;
            return new SerializedItemDescriptor(itemDesc.Version,
                item is null ? null : kind.Serialize(item));
        }

        private ItemDescriptor Deserialize(DataKind kind, SerializedItemDescriptor serializedItemDesc)
        {
            var serializedItem = serializedItemDesc.SerializedItem;
            return new ItemDescriptor(serializedItemDesc.Version,
                serializedItem is null ? null : kind.Deserialize(serializedItem));
        }
    }
    
    internal struct CacheKey : IEquatable<CacheKey>
    {
        public readonly DataKind Kind;
        public readonly string Key;

        public CacheKey(DataKind kind, string key)
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

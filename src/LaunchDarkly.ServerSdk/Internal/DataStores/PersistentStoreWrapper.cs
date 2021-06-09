using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Cache;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
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
        private readonly IDataStoreUpdates _dataStoreUpdates;
        private readonly Logger _log;
        private readonly ICache<CacheKey, ItemDescriptor?> _itemCache;
        private readonly ICache<DataKind, ImmutableDictionary<string, ItemDescriptor>> _allCache;
        private readonly ISingleValueCache<bool> _initCache;
        private readonly bool _cacheIndefinitely;
        private readonly List<DataKind> _cachedDataKinds = new List<DataKind>();
        private readonly PersistentDataStoreStatusManager _statusManager;

        private volatile bool _inited;
        
        internal PersistentStoreWrapper(
            IPersistentDataStoreAsync coreAsync,
            DataStoreCacheConfig caching,
            IDataStoreUpdates dataStoreUpdates,
            TaskExecutor taskExecutor,
            Logger log
            ) :
            this(new PersistentStoreAsyncAdapter(coreAsync), caching, dataStoreUpdates, taskExecutor, log)
        { }

        internal PersistentStoreWrapper(
            IPersistentDataStore core,
            DataStoreCacheConfig caching,
            IDataStoreUpdates dataStoreUpdates,
            TaskExecutor taskExecutor,
            Logger log
            )
        {
            this._core = core;
            this._caching = caching;
            this._dataStoreUpdates = dataStoreUpdates;
            this._log = log;

            _cacheIndefinitely = caching.IsEnabled && caching.IsInfiniteTtl;
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

            _statusManager = new PersistentDataStoreStatusManager(
                !_cacheIndefinitely,
                true,
                this.PollAvailabilityAfterOutage,
                dataStoreUpdates.UpdateStatus,
                taskExecutor,
                log
                );
        }

        public bool StatusMonitoringEnabled => true;

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
            lock (_cachedDataKinds)
            {
                _cachedDataKinds.Clear();
                foreach (var kv in items.Data)
                {
                    _cachedDataKinds.Add(kv.Key);
                }
            }

            var serializedItems = items.Data.ToImmutableDictionary(
                kindAndItems => kindAndItems.Key,
                kindAndItems => SerializeAll(kindAndItems.Key, kindAndItems.Value.Items)
            );
            Exception failure = InitCore(new FullDataSet<SerializedItemDescriptor>(serializedItems));
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
                    _allCache.Set(kind, e0.Value.Items.ToImmutableDictionary());
                    foreach (var e1 in e0.Value.Items)
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
        
        public ItemDescriptor? Get(DataKind kind, string key)
        {
            try
            {
                var ret = _itemCache is null ? GetAndDeserializeItem(kind, key) :
                    _itemCache.Get(new CacheKey(kind, key));
                ProcessError(null);
                return ret;
            }
            catch (Exception e)
            {
                ProcessError(e);
                throw;
            }
        }

        public KeyedItems<ItemDescriptor> GetAll(DataKind kind)
        {
            try
            {
                var ret = new KeyedItems<ItemDescriptor>(_allCache is null ?
                    GetAllAndDeserialize(kind) : _allCache.Get(kind));
                ProcessError(null);
                return ret;
            }
            catch (Exception e)
            {
                ProcessError(e);
                throw;
            }
        }

        public bool Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            var serializedItem = Serialize(kind, item);
            bool updated = false;
            Exception failure = null;
            try
            {
                updated = _core.Upsert(kind, key, serializedItem);
                ProcessError(null);
            }
            catch (Exception e)
            {
                // Normally, if the underlying store failed to do the update, we do not want to update the cache -
                // the idea being that it's better to stay in a consistent state of having old data than to act
                // like we have new data but then suddenly fall back to old data when the cache expires. However,
                // if the cache TTL is infinite, then it makes sense to update the cache always.
                ProcessError(e);
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
                _statusManager.Dispose();
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
            return _core.GetAll(kind).Items.ToImmutableDictionary(
                kv => kv.Key,
                kv => Deserialize(kind, kv.Value));

        }

        private SerializedItemDescriptor Serialize(DataKind kind, ItemDescriptor itemDesc)
        {
            return new SerializedItemDescriptor(itemDesc.Version,
                itemDesc.Item is null, kind.Serialize(itemDesc));
        }

        private KeyedItems<SerializedItemDescriptor> SerializeAll(DataKind kind,
            IEnumerable<KeyValuePair<string, ItemDescriptor>> items)
        {
            var itemsBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, SerializedItemDescriptor>>();
            foreach (var kv in items)
            {
                itemsBuilder.Add(new KeyValuePair<string, SerializedItemDescriptor>(kv.Key,
                    Serialize(kind, kv.Value)));
            }
            return new KeyedItems<SerializedItemDescriptor>(itemsBuilder.ToImmutable());
        }

        private ItemDescriptor Deserialize(DataKind kind, SerializedItemDescriptor serializedItemDesc)
        {
            if (serializedItemDesc.Deleted || serializedItemDesc.SerializedItem is null)
            {
                return ItemDescriptor.Deleted(serializedItemDesc.Version);
            }
            var deserializedItem = kind.Deserialize(serializedItemDesc.SerializedItem);
            if (serializedItemDesc.Version == 0 || serializedItemDesc.Version == deserializedItem.Version
                || deserializedItem.Item is null)
            {
                return deserializedItem;
            }
            // If the store gave us a version number that isn't what was encoded in the object, trust it
            return new ItemDescriptor(serializedItemDesc.Version, deserializedItem.Item);
        }

        private Exception InitCore(FullDataSet<SerializedItemDescriptor> allData)
        {
            try
            {
                _core.Init(allData);
                ProcessError(null);
                return null;
            }
            catch (Exception e)
            {
                ProcessError(e);
                return e;
            }
        }

        private void ProcessError(Exception e)
        {
            if (e == null)
            {
                // If we're waiting to recover after a failure, we'll let the polling routine take care
                // of signaling success. Even if we could signal success a little earlier based on the
                // success of whatever operation we just did, we'd rather avoid the overhead of acquiring
                // w.statusLock every time we do anything. So we'll just do nothing here.
                return;
            }
            _log.Error("Error from persistent data store: {0}", LogValues.ExceptionSummary(e));
            _statusManager.UpdateAvailability(false);
        }

        private bool PollAvailabilityAfterOutage()
        {
            if (!_core.IsStoreAvailable())
            {
                return false;
            }

            if (_cacheIndefinitely && _allCache != null)
            {
                // If we're in infinite cache mode, then we can assume the cache has a full set of current
                // flag data (since presumably the data source has still been running) and we can just
                // write the contents of the cache to the underlying data store.
                DataKind[] allKinds;
                lock (_cachedDataKinds)
                {
                    allKinds = _cachedDataKinds.ToArray();
                }
                var builder = ImmutableList.CreateBuilder<KeyValuePair<DataKind, KeyedItems<SerializedItemDescriptor>>>();
                foreach (var kind in allKinds)
                {
                    if (_allCache.TryGetValue(kind, out var items))
                    {
                        builder.Add(new KeyValuePair<DataKind, KeyedItems<SerializedItemDescriptor>>(kind,
                            SerializeAll(kind, items)));
                    }
                }
                var e = InitCore(new FullDataSet<SerializedItemDescriptor>(builder.ToImmutable()));
                if (e is null)
                {
                    _log.Warn("Successfully updated persistent store from cached data");
                }
                else
                {
                    // We failed to write the cached data to the underlying store. In this case, we should not
                    // return to a recovered state, but just try this all again next time the poll task runs.
                    LogHelpers.LogException(_log,
                        "Tried to write cached data to persistent store after a store outage, but failed",
                        e);
                    return false;
                }
            }

            return true;
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

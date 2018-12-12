using System;
using System.Collections.Generic;
using System.Linq;

namespace LaunchDarkly.Client.Utils
{
    /// <summary>
    /// CachingStoreWrapper is a partial implementation of <see cref="IFeatureStore"/> that delegates
    /// the basic functionality to an instance of <see cref="IFeatureStoreCore"/>. It provides optional
    /// caching behavior and other logic that would otherwise be repeated in every feature store
    /// implementation. This makes it easier to create new database integrations by implementing only
    /// the database-specific logic.
    /// 
    /// Construct instances of this class with <see cref="CachingStoreWrapper.Builder(IFeatureStoreCore)"/>.
    /// </summary>
    public sealed class CachingStoreWrapper : IFeatureStore
    {
        private readonly IFeatureStoreCore _core;
        private readonly FeatureStoreCaching _caching;
        
        private readonly LoadingCache<CacheKey, IVersionedData> _itemCache;
        private readonly LoadingCache<IVersionedDataKind, IDictionary<string, IVersionedData>> _allCache;
        private readonly LoadingCache<string, string> _initCache;
        private volatile bool _inited;

        /// <summary>
        /// Creates a new builder.
        /// </summary>
        /// <param name="core">the <see cref="IFeatureStoreCore"/> implementation</param>
        /// <returns>a builder</returns>
        public static CachingStoreWrapperBuilder Builder(IFeatureStoreCore core)
        {
            return new CachingStoreWrapperBuilder(core);
        }

        /// <summary>
        /// Creates a new builder using an asynchronous implementation.
        /// </summary>
        /// <param name="coreAsync">the <see cref="IFeatureStoreCoreAsync"/> implementation</param>
        /// <returns>a builder</returns>
        public static CachingStoreWrapperBuilder Builder(IFeatureStoreCoreAsync coreAsync)
        {
            return new CachingStoreWrapperBuilder(new FeatureStoreCoreAsyncAdapter(coreAsync));
        }

        internal CachingStoreWrapper(IFeatureStoreCore core, FeatureStoreCaching caching)
        {
            this._core = core;
            this._caching = caching;

            if (caching.IsEnabled)
            {
                _itemCache = new LoadingCache<CacheKey, IVersionedData>(GetInternalForCache, caching.Ttl);
                _allCache = new LoadingCache<IVersionedDataKind, IDictionary<string, IVersionedData>>(GetAllForCache, caching.Ttl);
                _initCache = new LoadingCache<string, string>(GetInitedStateForCache, caching.Ttl);
            }
            else
            {
                _itemCache = null;
                _allCache = null;
                _initCache = null;
            }
        }

        /// <summary>
        /// <see cref="IFeatureStore.Initialized"/>
        /// </summary>
        public bool Initialized()
        {
            if (_inited)
            {
                return true;
            }
            bool result;
            if (_initCache != null)
            {
                result = _initCache.Get("arbitrary-key") != null;
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

        /// <summary>
        /// <see cref="IFeatureStore.Init"/>
        /// </summary>
        public void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> items)
        {
            _core.InitInternal(items);
            _inited = true;
            if (_itemCache != null && _allCache != null)
            {
                _itemCache.Clear();
                _allCache.Clear();
                foreach (var e0 in items)
                {
                    var kind = e0.Key;
                    _allCache.Set(kind, e0.Value);
                    foreach (var e1 in e0.Value)
                    {
                        _itemCache.Set(new CacheKey(kind, e1.Key), e1.Value);
                    }
                }
            }
        }

        /// <summary>
        /// <see cref="IFeatureStore.Get"/>
        /// </summary>
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

        /// <summary>
        /// <see cref="IFeatureStore.All"/>
        /// </summary>
        public IDictionary<string, T> All<T>(VersionedDataKind<T> kind) where T : class, IVersionedData
        {
            if (_allCache != null)
            {
                return FilterItems<T>(_allCache.Get(kind));
            }
            return FilterItems<T>(_core.GetAllInternal(kind));
        }

        /// <summary>
        /// <see cref="IFeatureStore.Upsert"/>
        /// </summary>
        public void Upsert<T>(VersionedDataKind<T> kind, T item) where T : IVersionedData
        {
            IVersionedData newState = _core.UpsertInternal(kind, item);
            if (_itemCache != null)
            {
                _itemCache.Set(new CacheKey(kind, item.Key), newState);
            }
            if (_allCache != null)
            {
                _allCache.Remove(kind);
            }
        }

        /// <summary>
        /// <see cref="IFeatureStore.Delete"/>
        /// </summary>
        public void Delete<T>(VersionedDataKind<T> kind, string key, int version) where T : IVersionedData
        {
            Upsert(kind, kind.MakeDeletedItem(key, version));
        }

        /// <see cref="IDisposable.Dispose"/>
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

        private IDictionary<string, IVersionedData> GetAllForCache(IVersionedDataKind kind)
        {
            return _core.GetAllInternal(kind);
        }

        private IDictionary<string, T> FilterItems<T>(IDictionary<string, IVersionedData> items) where T : IVersionedData
        {
            return items.Where(kv => !kv.Value.Deleted).ToDictionary(i => i.Key, i => (T)i.Value);
        }

        private string GetInitedStateForCache(string key)
        {
            return _core.InitializedInternal() ? key : null;
        }
    }

    /// <summary>
    /// Builder class for <see cref="CachingStoreWrapper"/>.
    /// </summary>
    public class CachingStoreWrapperBuilder
    {
        private readonly IFeatureStoreCore _core;
        private FeatureStoreCaching _caching = FeatureStoreCaching.Enabled;

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
        /// <param name="caching">a <see cref="FeatureStoreCaching"/> object</param>
        /// <returns>the builder</returns>
        public CachingStoreWrapperBuilder WithCaching(FeatureStoreCaching caching)
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

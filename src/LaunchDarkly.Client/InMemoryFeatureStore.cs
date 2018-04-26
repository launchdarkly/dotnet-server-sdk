using System;
using System.Collections.Generic;
using System.Threading;
using Common.Logging;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// In-memory, thread-safe implementation of IFeatureStore.
    /// 
    /// Referencing this class directly is deprecated; please use <see cref="Implementations.InMemoryFeatureStore"/>
    /// instead.
    /// </summary>
    public class InMemoryFeatureStore : IFeatureStore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(InMemoryFeatureStore));
        private static readonly int RwLockMaxWaitMillis = 1000;
        private readonly ReaderWriterLockSlim RwLock = new ReaderWriterLockSlim();
        private readonly IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> Items =
            new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
        private bool _initialized = false;

        public InMemoryFeatureStore() { }

        /// <see cref="IFeatureStore.Get{T}(VersionedDataKind{T}, string)"/>
        public T Get<T>(VersionedDataKind<T> kind, string key) where T : class, IVersionedData
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                IDictionary<string, IVersionedData> itemsOfKind;
                IVersionedData item;

                if (!Items.TryGetValue(kind, out itemsOfKind))
                {
                    Log.DebugFormat("Key {0} not found in '{1}'; returning null", key, kind.GetNamespace());
                    return null;
                }
                if (!itemsOfKind.TryGetValue(key, out item))
                {
                    Log.DebugFormat("Key {0} not found in '{1}'; returning null", key, kind.GetNamespace());
                    return null;
                }
                if (item.Deleted)
                {
                    Log.WarnFormat("Attempted to get deleted item with key {0} in '{1}'; returning null.",
                        key, kind.GetNamespace());
                    return null;
                }
                return (T)item;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        /// <see cref="IFeatureStore.All{T}(VersionedDataKind{T})"/>
        public IDictionary<string, T> All<T>(VersionedDataKind<T> kind) where T : class, IVersionedData
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                IDictionary<string, T> ret = new Dictionary<string, T>();
                IDictionary<string, IVersionedData> itemsOfKind;
                if (Items.TryGetValue(kind, out itemsOfKind))
                {
                    foreach (var entry in itemsOfKind)
                    {
                        if (!entry.Value.Deleted)
                        {
                            ret[entry.Key] = (T)entry.Value;
                        }
                    }
                }
                return ret;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        /// <see cref="IFeatureStore.Init(IDictionary{IVersionedDataKind, IDictionary{string, IVersionedData}})"/>
        public void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> items)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                Items.Clear();
                foreach (var kindEntry in items)
                {
                    IDictionary<string, IVersionedData> itemsOfKind = new Dictionary<string, IVersionedData>();
                    foreach (var e1 in kindEntry.Value)
                    {
                        itemsOfKind[e1.Key] = e1.Value;
                    }
                    Items[kindEntry.Key] = itemsOfKind;
                }
                _initialized = true;
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        /// <see cref="IFeatureStore.Delete{T}(VersionedDataKind{T}, string, int)"/>
        public void Delete<T>(VersionedDataKind<T> kind, string key, int version) where T : IVersionedData
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                IDictionary<string, IVersionedData> itemsOfKind;
                if (Items.TryGetValue(kind, out itemsOfKind))
                {
                    IVersionedData item;
                    if (!itemsOfKind.TryGetValue(key, out item) || item.Version < version)
                    {
                        itemsOfKind[key] = kind.MakeDeletedItem(key, version);
                    }
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        /// <see cref="IFeatureStore.Upsert{T}(VersionedDataKind{T}, T)"/>
        public void Upsert<T>(VersionedDataKind<T> kind, T item) where T : IVersionedData
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                IDictionary<string, IVersionedData> itemsOfKind;
                if (!Items.TryGetValue(kind, out itemsOfKind))
                {
                    itemsOfKind = new Dictionary<string, IVersionedData>();
                    Items[kind] = itemsOfKind;
                }
                IVersionedData old;
                if (!itemsOfKind.TryGetValue(item.Key, out old) || old.Version < item.Version)
                {
                    itemsOfKind[item.Key] = item;
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        /// <see cref="IFeatureStore.Initialized"/>
        public bool Initialized()
        {
            return _initialized;
        }

        public void Dispose()
        { }
    }
}
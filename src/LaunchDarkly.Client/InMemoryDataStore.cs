using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.Client
{
    public abstract class InMemoryDataStore<T> where T : class, IVersionedData
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<InMemoryFeatureStore>();
        private static readonly int RwLockMaxWaitMillis = 1000;
        private readonly ReaderWriterLockSlim RwLock = new ReaderWriterLockSlim();
        private readonly IDictionary<string, T> Items = new Dictionary<string, T>();
        private bool _initialized = false;

        public T Get(string key)
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                T item;

                if (!Items.TryGetValue(key, out item))
                {
                    Logger.LogWarning("Attempted to get {1} with key: {0} not found in {1} store. Returning null.",
                        key, ItemName());
                    return null;
                }
                if (item.Deleted)
                {
                    Logger.LogWarning("Attempted to get deleted {1} with key:{0} from {1} store. Returning null.",
                        key, ItemName());
                    return null;
                }
                return item;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        public IDictionary<string, T> All()
        {
            try
            {
                RwLock.TryEnterReadLock(RwLockMaxWaitMillis);
                IDictionary<string, T> ret = new Dictionary<string, T>();
                foreach (var entry in Items)
                {
                    if (!entry.Value.Deleted)
                    {
                        ret[entry.Key] = entry.Value;
                    }
                }
                return ret;
            }
            finally
            {
                RwLock.ExitReadLock();
            }
        }

        public void Init(IDictionary<string, T> items)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                Items.Clear();
                foreach (var entry in items)
                {
                    Items[entry.Key] = entry.Value;
                }
                _initialized = true;
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        public void Delete(string key, int version)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                T item;
                if (Items.TryGetValue(key, out item) && item.Version < version)
                {
                    item.Deleted = true;
                    item.Version = version;
                    Items[key] = item;
                }
                else if (item == null)
                {
                    item = EmptyItem();
                    item.Deleted = true;
                    item.Version = version;
                    Items[key] = item;
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        public void Upsert(string key, T item)
        {
            try
            {
                RwLock.TryEnterWriteLock(RwLockMaxWaitMillis);
                T old;
                if (!Items.TryGetValue(key, out old) || old.Version < item.Version)
                {
                    Items[key] = item;
                }
            }
            finally
            {
                RwLock.ExitWriteLock();
            }
        }

        public bool Initialized()
        {
            return _initialized;
        }

        protected abstract T EmptyItem();

        protected abstract string ItemName();
    }
}
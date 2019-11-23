using System.Collections.Generic;
using System.Collections.Immutable;
using Common.Logging;
using LaunchDarkly.Client.Interfaces;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// In-memory, thread-safe implementation of <see cref="IDataStore"/>.
    /// </summary>
    internal class InMemoryDataStore : IDataStore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(InMemoryDataStore));
        private readonly object WriterLock = new object();
        private volatile ImmutableDictionary<IVersionedDataKind, ImmutableDictionary<string, IVersionedData>> Items =
            ImmutableDictionary<IVersionedDataKind, ImmutableDictionary<string, IVersionedData>>.Empty;
        private volatile bool _initialized = false;

        /// <summary>
        /// Creates a new empty data store instance.
        /// </summary>
        public InMemoryDataStore() { }

        public T Get<T>(VersionedDataKind<T> kind, string key) where T : class, IVersionedData
        {
            ImmutableDictionary<string, IVersionedData> itemsOfKind;
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

        public IDictionary<string, T> All<T>(VersionedDataKind<T> kind) where T : class, IVersionedData
        {
            IDictionary<string, T> ret = new Dictionary<string, T>();
            ImmutableDictionary<string, IVersionedData> itemsOfKind;
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

        public void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> items)
        {
            lock (WriterLock)
            {
                Items = CreateImmutableItems(items);
                _initialized = true;
            }
        }

        public void Delete<T>(VersionedDataKind<T> kind, string key, int version) where T : IVersionedData
        {
            lock (WriterLock)
            {
                ImmutableDictionary<string, IVersionedData> itemsOfKind;
                if (Items.TryGetValue(kind, out itemsOfKind))
                {
                    IVersionedData item;
                    if (!itemsOfKind.TryGetValue(key, out item) || item.Version < version)
                    {
                        ImmutableDictionary<string, IVersionedData> newItemsOfKind = itemsOfKind.SetItem(key, kind.MakeDeletedItem(key, version));
                        Items = Items.SetItem(kind, newItemsOfKind);
                    }
                }
            }
        }

        public void Upsert<T>(VersionedDataKind<T> kind, T item) where T : IVersionedData
        {
            lock (WriterLock)
            {
                ImmutableDictionary<string, IVersionedData> itemsOfKind;
                if (!Items.TryGetValue(kind, out itemsOfKind))
                {
                    itemsOfKind = ImmutableDictionary<string, IVersionedData>.Empty;
                }
                IVersionedData old;
                if (!itemsOfKind.TryGetValue(item.Key, out old) || old.Version < item.Version)
                {
                    ImmutableDictionary<string, IVersionedData> newItemsOfKind = itemsOfKind.SetItem(item.Key, item);
                    Items = Items.SetItem(kind, newItemsOfKind);
                }
            }
        }

        public bool Initialized()
        {
            return _initialized;
        }

        public void Dispose() { }

        private static ImmutableDictionary<IVersionedDataKind, ImmutableDictionary<string, IVersionedData>> CreateImmutableItems(
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> items
        )
        {
            var itemsBuilder = ImmutableDictionary.CreateBuilder<IVersionedDataKind, ImmutableDictionary<string, IVersionedData>>();

            foreach (var kindEntry in items)
            {
                var kindItemsBuilder = ImmutableDictionary.CreateBuilder<string, IVersionedData>();
                foreach (var e1 in kindEntry.Value)
                {
                    kindItemsBuilder.Add(e1.Key, e1.Value);
                }

                itemsBuilder.Add(kindEntry.Key, kindItemsBuilder.ToImmutable());
            }

            return itemsBuilder.ToImmutable();
        }
    }
}
using System.Collections.Immutable;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// In-memory, thread-safe implementation of <see cref="IDataStore"/>.
    /// </summary>
    /// <remarks>
    /// Application code cannot see this implementation class and uses
    /// <see cref="Components.InMemoryDataStore"/> instead.
    /// </remarks>
    internal class InMemoryDataStore : IDataStore
    {
        private readonly object WriterLock = new object();
        private volatile ImmutableDictionary<DataKind, ImmutableDictionary<string, ItemDescriptor>> Items =
            ImmutableDictionary<DataKind, ImmutableDictionary<string, ItemDescriptor>>.Empty;
        private volatile bool _initialized = false;

        internal InMemoryDataStore() { }

        public bool StatusMonitoringEnabled => false;

        public void Init(FullDataSet<ItemDescriptor> data)
        {
            var itemsBuilder = ImmutableDictionary.CreateBuilder<DataKind, ImmutableDictionary<string, ItemDescriptor>>();

            foreach (var kindEntry in data.Data)
            {
                var kindItemsBuilder = ImmutableDictionary.CreateBuilder<string, ItemDescriptor>();
                foreach (var e1 in kindEntry.Value.Items)
                {
                    kindItemsBuilder.Add(e1.Key, e1.Value);
                }

                itemsBuilder.Add(kindEntry.Key, kindItemsBuilder.ToImmutable());
            }

            var newItems = itemsBuilder.ToImmutable();

            lock (WriterLock)
            {
                Items = newItems;
                _initialized = true;
            }
        }

        public ItemDescriptor? Get(DataKind kind, string key)
        {
            if (!Items.TryGetValue(kind, out var itemsOfKind))
            {
                return null;
            }
            if (!itemsOfKind.TryGetValue(key, out var item))
            {
                return null;
            }
            return item;
        }

        public KeyedItems<ItemDescriptor> GetAll(DataKind kind)
        {
            if (Items.TryGetValue(kind, out var itemsOfKind))
            {
                return new KeyedItems<ItemDescriptor>(itemsOfKind);
            }
            return KeyedItems<ItemDescriptor>.Empty();
        }
        
        public bool Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            lock (WriterLock)
            {
                if (!Items.TryGetValue(kind, out var itemsOfKind))
                {
                    itemsOfKind = ImmutableDictionary<string, ItemDescriptor>.Empty;
                }
                if (!itemsOfKind.TryGetValue(key, out var old) || old.Version < item.Version)
                {
                    var newItemsOfKind = itemsOfKind.SetItem(key, item);
                    Items = Items.SetItem(kind, newItemsOfKind);
                    return true;
                }
                return false;
            }
        }

        public bool Initialized()
        {
            return _initialized;
        }

        public void Dispose() { }
    }
}
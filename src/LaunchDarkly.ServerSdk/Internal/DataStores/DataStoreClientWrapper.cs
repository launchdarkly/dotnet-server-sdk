using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// Provides additional behavior that the client requires before or after data store operations.
    /// Currently this just means sorting the data set for Init(). In the future we may also use this
    /// to provide an update listener capability.
    /// </summary>
    internal class DataStoreClientWrapper : IDataStore
    {
        private readonly IDataStore _store;

        internal DataStoreClientWrapper(IDataStore store)
        {
            _store = store;
        }

        public void Init(FullDataSet<ItemDescriptor> allData)
        {
            _store.Init(DataStoreSorter.SortAllCollections(allData));
        }

        public ItemDescriptor? Get(DataKind kind, string key)
        {
            return _store.Get(kind, key);
        }

        public IEnumerable<KeyValuePair<string, ItemDescriptor>> GetAll(DataKind kind)
        {
            return _store.GetAll(kind);
        }

        public bool Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            return _store.Upsert(kind, key, item);
        }
        
        public bool Initialized()
        {
            return _store.Initialized();
        }

        public void Dispose()
        {
            _store.Dispose();
        }
    }
}

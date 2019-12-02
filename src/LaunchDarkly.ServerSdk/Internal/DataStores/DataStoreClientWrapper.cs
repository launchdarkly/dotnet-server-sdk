using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Interfaces;

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

        public void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            _store.Init(DataStoreDataSetSorter.SortAllCollections(allData));
        }

        public T Get<T>(VersionedDataKind<T> kind, string key) where T : class, IVersionedData
        {
            return _store.Get(kind, key);
        }

        public IDictionary<string, T> All<T>(VersionedDataKind<T> kind) where T : class, IVersionedData
        {
            return _store.All(kind);
        }

        public void Upsert<T>(VersionedDataKind<T> kind, T item) where T : IVersionedData
        {
            _store.Upsert(kind, item);
        }
        
        public void Delete<T>(VersionedDataKind<T> kind, string key, int version) where T : IVersionedData
        {
            _store.Delete(kind, key, version);
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

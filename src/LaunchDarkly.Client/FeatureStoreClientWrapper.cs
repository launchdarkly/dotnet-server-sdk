using System.Collections.Generic;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Provides additional behavior that the client requires before or after feature store operations.
    /// Currently this just means sorting the data set for Init(). In the future we may also use this
    /// to provide an update listener capability.
    /// </summary>
    internal class FeatureStoreClientWrapper : IFeatureStore
    {
        private readonly IFeatureStore _store;

        internal FeatureStoreClientWrapper(IFeatureStore store)
        {
            _store = store;
        }

        public void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            _store.Init(FeatureStoreDataSetSorter.SortAllCollections(allData));
        }

        T IFeatureStore.Get<T>(VersionedDataKind<T> kind, string key)
        {
            return _store.Get(kind, key);
        }

        IDictionary<string, T> IFeatureStore.All<T>(VersionedDataKind<T> kind)
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

using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataStores
{
    /// <summary>
    /// Provides additional behavior that the client requires when the data source has provided some
    /// new data to put into the data store.
    /// </summary>
    /// <remarks>
    /// Currently this just means sorting the data set for Init(). In the future we may also use this
    /// to provide an update listener capability.
    /// </remarks>
    internal class DataSourceUpdatesImpl : IDataSourceUpdates
    {
        private readonly IDataStore _store;

        internal DataSourceUpdatesImpl(IDataStore store)
        {
            _store = store;
        }

        public void Init(FullDataSet<ItemDescriptor> allData)
        {
            _store.Init(DataStoreSorter.SortAllCollections(allData));
        }
        
        public void Upsert(DataKind kind, string key, ItemDescriptor item)
        {
            _store.Upsert(kind, key, item);
        }

        public void Dispose()
        {
            _store.Dispose();
        }
    }
}

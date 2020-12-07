using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface that an implementation of <see cref="IDataSource"/> will use to put data into the
    /// underlying data store.
    /// </summary>
    /// <remarks>
    /// These methods correspond to the <see cref="IDataStore.Init(FullDataSet{ItemDescriptor})"/> and
    /// <see cref="IDataStore.Upsert(DataKind, string, ItemDescriptor)"/> methods in <see cref="IDataStore"/>,
    /// but the data source does not have full access to the data store; all it can do is provide new data
    /// through these methods. The SDK may modify the data before sending it to the store, or take other
    /// actions when an update happens, that are separate from the store implementation.
    /// </remarks>
    public interface IDataSourceUpdates
    {
        /// <summary>
        /// Provides a full data set to be copied into the data store, overwriting any previous contents.
        /// </summary>
        /// <param name="allData">a list of <see cref="DataKind"/> instances and their
        /// corresponding data sets</param>
        void Init(FullDataSet<ItemDescriptor> allData);

        /// <summary>
        /// Adds or replaces a single data item.
        /// </summary>
        /// <remarks>
        /// The <see cref="ItemDescriptor"/> may contain a null, to represent a placeholder for a deleted item.
        /// </remarks>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique key for the item within that collection</param>
        /// <param name="item">the item to insert or update</param>
        void Upsert(DataKind kind, string key, ItemDescriptor item);
    }
}

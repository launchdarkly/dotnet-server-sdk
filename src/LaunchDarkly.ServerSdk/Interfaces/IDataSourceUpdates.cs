using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface that an implementation of <see cref="IDataSource"/> will use to push data into the SDK.
    /// </summary>
    /// <remarks>
    /// The data source interacts with this object, rather than manipulating the data store directly, so
    /// that the SDK can perform any other necessary operations that must happen when data is updated. This
    /// object also provides a mechanism to report status changes.
    /// </remarks>
    public interface IDataSourceUpdates
    {
        /// <summary>
        /// An object that provides status tracking for the data store, if applicable.
        /// </summary>
        /// <remarks>
        /// This may be useful if the data source needs to be aware of storage problems that might require it
        /// to take some special action: for instance, if a database outage may have caused some data to be
        /// lost and therefore the data should be re-requested from LaunchDarkly.
        /// </remarks>
        IDataStoreStatusProvider DataStoreStatusProvider { get; }

        /// <summary>
        /// Completely overwrites the current contents of the data store with a set of items for each collection.
        /// </summary>
        /// <param name="allData">a list of <see cref="DataKind"/> instances and their
        /// corresponding data sets</param>
        /// <returns>true if the update succeeded, false if it failed</returns>
        bool Init(FullDataSet<ItemDescriptor> allData);

        /// <summary>
        /// Updates or inserts an item in the specified collection. For updates, the object will only be
        /// updated if the existing version is less than the new version.
        /// </summary>
        /// <remarks>
        /// The <see cref="ItemDescriptor"/> may contain a null, to represent a placeholder for a deleted item.
        /// </remarks>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique key for the item within that collection</param>
        /// <param name="item">the item to insert or update</param>
        /// <returns>true if the update succeeded, false if it failed</returns>
        bool Upsert(DataKind kind, string key, ItemDescriptor item);

        /// <summary>
        /// Informs the SDK of a change in the data source's status.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Data source implementations should use this method if they have any concept of being in a valid
        /// state, a temporarily disconnected state, or a permanently stopped state.
        /// </para>
        /// <para>
        /// If <paramref name="newState"/> is different from the previous state, and/or <paramref name="newError"/>
        /// is non-null, the SDK will start returning the new status(adding a timestamp for the change) from
        /// <see cref="IDataSourceStatusProvider.Status"/>, and will trigger status change events to any
        /// registered listeners.
        /// </para>
        /// <para>
        /// A special case is that if <paramref name="newState"/> is <see cref="DataSourceState.Interrupted"/>,
        /// but the previous state was <see cref="DataSourceState.Initializing"/>, the state will
        /// remain at <see cref="DataSourceState.Initializing"/> because
        /// <see cref="DataSourceState.Interrupted"/> is only meaningful after a successful startup.
        /// </para>
        /// </remarks>
        /// <param name="newState">the data source state</param>
        /// <param name="newError">information about a new error, if any</param>
        /// <seealso cref="IDataSourceStatusProvider"/>
        void UpdateStatus(DataSourceState newState, DataSourceStatus.ErrorInfo? newError);
    }
}

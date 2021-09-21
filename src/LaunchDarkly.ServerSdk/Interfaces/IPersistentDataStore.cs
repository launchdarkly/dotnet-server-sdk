using System;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a data store that holds feature flags and related data in a
    /// serialized form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface should be used for database integrations, or any other data store
    /// implementation that stores data in some external service. The SDK will take care of
    /// converting between its own internal data model and a serialized string form; the
    /// data store interacts only with the serialized form.
    /// </para>
    /// <para>
    /// The SDK will also provide its own caching layer on top of the persistent data
    /// store; the data store implementation should not provide caching, but simply do
    /// every query or update that the SDK tells it to do.
    /// </para>
    /// <para>
    /// Implementations must be thread-safe.
    /// </para>
    /// <para>
    /// Implementations that use a task-based asynchronous pattern can use
    /// <see cref="IPersistentDataStoreAsync"/> instead.
    /// </para>
    /// <para>
    /// Conceptually, each item in the store is a <see cref="SerializedItemDescriptor"/> which
    /// always has a version number, and can represent either a serialized object or a
    /// placeholder (tombstone) for a deleted item. There are two approaches a persistent store
    /// implementation can use for persisting this data:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// Preferably, it should store the version number and the <see cref="SerializedItemDescriptor.Deleted"/>
    /// state separately so that the object does not need to be fully deserialized to read
    /// them. In this case, deleted item placeholders can ignore the value of
    /// <see cref="SerializedItemDescriptor.SerializedItem"/> on writes and can set it to
    /// null on reads. The store should never call <see cref="DataKind.Deserialize(string)"/>
    /// or <see cref="DataKind.Serialize(ItemDescriptor)"/> in this case.
    /// </description></item>
    /// <item><description>
    /// If that isn't possible, then the store should simply persist the exact string from
    /// <see cref="SerializedItemDescriptor.SerializedItem"/> on writes, and return the persisted
    /// string on reads -- setting <see cref="SerializedItemDescriptor.Version"/> to zero and
    /// <see cref="SerializedItemDescriptor.Deleted"/> to false. The string is guaranteed to
    /// provide the SDK with enough information to infer the version and the deleted state.
    /// On updates, the store will have to call <see cref="DataKind.Deserialize(string)"/> in
    /// order to inspect the version number of the existing item if any.
    /// </description></item>
    /// </list>
    /// <para>
    /// Error handling is defined as follows: if any data store operation encounters a database
    /// error, or is otherwise unable to complete its task, it should throw an exception to make
    /// the SDK aware of this. The SDK will log the exception and will assume that the data store
    /// is now in a non-operational state; the SDK will then start polling <see cref="IsStoreAvailable"/>
    /// to determine when the store has started working again.
    /// </para>
    /// </remarks>
    /// <seealso cref="IPersistentDataStoreFactory"/>
    /// <seealso cref="IPersistentDataStoreAsync"/>
    /// <seealso cref="IDataStore"/>
    public interface IPersistentDataStore : IDisposable
    {
        /// <summary>
        /// Overwrites the store's contents with a set of items for each collection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All previous data should be discarded, regardless of versioning.
        /// </para>
        /// <para>
        /// The update should be done atomically. If it cannot be done atomically, then the store
        /// must first add or update each item in the same order that they are given in the input
        /// data, and then delete any previously stored items that were not in the input data.
        /// </para>
        /// </remarks>
        /// <param name="allData">a list of <see cref="DataKind"/> instances and their
        /// corresponding data sets</param>
        void Init(FullDataSet<SerializedItemDescriptor> allData);

        /// <summary>
        /// Retrieves an item from the specified collection, if available.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the key is not known at all, the method should return null. Otherwise, it should return
        /// a <see cref="SerializedItemDescriptor"/> as follows:
        /// </para>
        /// <list type="number">
        /// <item><description>
        /// If the version number and deletion state can be determined without fully deserializing
        /// the item, then the store should set those properties in the <see cref="SerializedItemDescriptor"/>
        /// (and can set <see cref="SerializedItemDescriptor.SerializedItem"/> to null for deleted items).
        /// </description></item>
        /// <item><description>
        /// Otherwise, it should simply set <see cref="SerializedItemDescriptor.SerializedItem"/> to
        /// the exact string that was persisted, and can leave the other properties as zero/false. The
        /// SDK will inspect the properties of the item after deserializing it to fill in the rest of
        /// the information.
        /// </description></item>
        /// </list>
        /// </remarks>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique key of the item within that collection</param>
        /// <returns>a versioned item that contains the stored data (or placeholder for
        /// deleted data); null if the key is unknown</returns>
        SerializedItemDescriptor? Get(DataKind kind, string key);

        /// <summary>
        /// Retrieves all items from the specified collection.
        /// </summary>
        /// <remarks>
        /// If the store contains placeholders for deleted items, it should include them in
        /// the results, not filter them out. See <see cref="Get(DataKind, string)"/> for how to set
        /// the properties of the <see cref="SerializedItemDescriptor"/> for each item.
        /// </remarks>
        /// <param name="kind">specifies which collection to use</param>
        /// <returns>a collection of key-value pairs; the ordering is not significant</returns>
        KeyedItems<SerializedItemDescriptor> GetAll(DataKind kind);

        /// <summary>
        /// Updates or inserts an item in the specified collection. For updates, the object will only be
        /// updated if the existing version is less than the new version.
        /// </summary>
        /// <remarks>
        /// The SDK may pass an <see cref="ItemDescriptor"/> that contains a null, to
        /// represent a placeholder for a deleted item. In that case, assuming the version
        /// is greater than any existing version of that item, the store should retain that
        /// placeholder rather than simply not storing anything.
        /// </remarks>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique key for the item within that collection</param>
        /// <param name="item">the item to insert or update</param>
        /// <returns>true if the item was updated; false if it was not updated because the
        /// store contains an equal or greater version</returns>
        bool Upsert(DataKind kind, string key, SerializedItemDescriptor item);

        /// <summary>
        /// Returns true if this store has been initialized.
        /// </summary>
        /// <remarks>
        /// In a shared data store, the implementation should be able to detect this
        /// state even if <see cref="Init(FullDataSet{SerializedItemDescriptor})"/> was called in a
        /// different process, i.e. it must query the underlying data store in some way. The method
        /// does not need to worry about caching this value; the SDK will call it rarely.
        /// </remarks>
        /// <returns>true if the store has been initialized</returns>
        bool Initialized();

        /// <summary>
        /// Tests whether the data store seems to be functioning normally.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This should not be a detailed test of different kinds of operations, but just the smallest
        /// possible operation to determine whether (for instance) we can reach the database.
        /// </para>
        /// <para>
        /// Whenever one of the store's other methods throws an exception, the SDK will assume that it
        /// may have become unavailable (e.g. the database connection was lost). The SDK will then call
        /// <c>IsStoreAvailable()</c> at intervals until it returns true.
        /// </para>
        /// </remarks>
        /// <returns>true if the underlying data store is reachable</returns>
        bool IsStoreAvailable();
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IPersistentDataStore"/>.
    /// </summary>
    /// <seealso cref="ConfigurationBuilder.DataStore(IDataStoreFactory)"/>
    /// <seealso cref="Components.PersistentDataStore(IPersistentDataStoreFactory)"/>
    public interface IPersistentDataStoreFactory
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <param name="context">configuration of the current client instance</param>
        /// <returns>an <see cref="IPersistentDataStore"/> instance</returns>
        IPersistentDataStore CreatePersistentDataStore(LdClientContext context);
    }
}

using System;
using System.Collections.Generic;

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
        /// If the item has been deleted and the store contains a placeholder, it should
        /// return that placeholder rather than null.
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
        /// the results, not filter them out.
        /// </remarks>
        /// <param name="kind">specifies which collection to use</param>
        /// <returns>a mapping of string keys to items</returns>
        IEnumerable<KeyValuePair<string, SerializedItemDescriptor>> GetAll(DataKind kind);

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
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IPersistentDataStore"/>.
    /// </summary>
    /// <seealso cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/>
    /// <seealso cref="Components.PersistentStore(IPersistentDataStoreFactory)"/>
    public interface IPersistentDataStoreFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <returns>a <see cref="IPersistentDataStore"/> instance</returns>
        IPersistentDataStore CreatePersistentDataStore();
    }
}

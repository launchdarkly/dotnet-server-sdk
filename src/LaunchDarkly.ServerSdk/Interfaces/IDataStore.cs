using System;
using System.Collections.Generic;
using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a data store that holds feature flags and related data received by the SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ordinarily, the only implementation of this interface is the default in-memory
    /// implementation, which holds references to actual SDK data model objects. Any data store
    /// implementation that uses an external store, such as a database, should instead use
    /// <see cref="IPersistentDataStore"/> or <see cref="IPersistentDataStoreAsync"/>.
    /// </para>
    /// <para>
    /// Implementations must be thread-safe.
    /// </para>
    /// </remarks>
    /// <seealso cref="IDataStoreFactory"/>
    /// <seealso cref="IPersistentDataStore"/>
    /// <seealso cref="IPersistentDataStoreAsync"/>
    public interface IDataStore : IDisposable
    {
        /// <summary>
        /// True if this data store implementation supports status monitoring.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is normally only true for persistent data stores created with
        /// <see cref="Components.PersistentDataStore(IPersistentDataStoreFactory)"/>, but it could
        /// also be true for any custom <see cref="IDataStore"/> implementation that makes use of the
        /// <see cref="IDataStoreUpdates"/> mechanism. Returning true means that the store guarantees
        /// that if it ever enters an invalid state (that is, an operation has failed or it knows
        /// that operations cannot succeed at the moment), it will publish a status update, and will
        /// then publish another status update once it has returned to a valid state.
        /// </para>
        /// <para>
        /// The same value will be returned from
        /// <see cref="IDataStoreStatusProvider.StatusMonitoringEnabled"/>.
        /// </para>
        /// </remarks>
        bool StatusMonitoringEnabled { get; }

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
        void Init(FullDataSet<ItemDescriptor> allData);

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
        ItemDescriptor? Get(DataKind kind, string key);

        /// <summary>
        /// Retrieves all items from the specified collection.
        /// </summary>
        /// <remarks>
        /// If the store contains placeholders for deleted items, it should include them in
        /// the results, not filter them out.
        /// </remarks>
        /// <param name="kind">specifies which collection to use</param>
        /// <returns>a collection of key-value pairs; the ordering is not significant</returns>
        KeyedItems<ItemDescriptor> GetAll(DataKind kind);

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
        bool Upsert(DataKind kind, string key, ItemDescriptor item);

        /// <summary>
        /// Checks whether this store has been initialized with any data yet.
        /// </summary>
        /// <remarks>
        /// This is defined as a method rather than a property to emphasize that it may be an
        /// operation that involves I/O; some data stores need to do a database query to see if
        /// there is existing data.
        /// </remarks>
        /// <returns>true if the store contains data</returns>
        bool Initialized();
    }
}

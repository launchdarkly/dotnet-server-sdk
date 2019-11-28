using System;
using System.Collections.Generic;
using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a data store that holds feature flags and related data received by the streaming client.
    /// </summary>
    /// <seealso cref="IDataStoreFactory"/>
    public interface IDataStore : IDisposable
    {
        /// <summary>
        /// Overwrite the store's contents with a set of objects for each collection.
        /// </summary>
        /// <param name="allData">a dictionary where each key specifies a collection, and each value
        /// is a map of string keys to objects in that collection</param>
        void Init(FullDataSet<ItemDescriptor> allData);

        /// <summary>
        /// Retrieve an object from the specified collection, or return null if not found.
        /// </summary>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique string key of the object</param>
        /// <returns>the found object or null</returns>
        ItemDescriptor? Get(DataKind kind, string key);

        /// <summary>
        /// Retrieve all objects from the specified collection.
        /// </summary>
        /// <param name="kind">specifies which collection to use</param>
        /// <returns>a dictionary of string keys to objects</returns>
        IEnumerable<KeyValuePair<string, ItemDescriptor>> GetAll(DataKind kind);

        /// <summary>
        /// Update or insert an object in the specified collection. For updates, the object will only be
        /// updated if the existing version is less than the new version.
        /// </summary>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="item">the item to insert or update</param>
        bool Upsert(DataKind kind, string key, ItemDescriptor item);

        /// <summary>
        /// Check whether this store has been initialized with any data yet.
        /// </summary>
        /// <returns>true if the store contains data</returns>
        bool Initialized();
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IDataStore"/>.
    /// </summary>
    /// <seealso cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/>
    /// <seealso cref="Components"/>
    public interface IDataStoreFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <returns>a <see cref="IDataStore"/> instance</returns>
        IDataStore CreateDataStore();
    }
}

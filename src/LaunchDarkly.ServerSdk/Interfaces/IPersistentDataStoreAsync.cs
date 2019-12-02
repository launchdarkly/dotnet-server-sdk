using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a data store that holds feature flags and related data in a
    /// serialized form.
    /// </summary>
    /// <remarks>
    /// This is exactly equivalent to <see cref="IPersistentDataStore"/>, but for
    /// implementations that use a task-based asynchronous pattern.
    /// </remarks>
    public interface IPersistentDataStoreAsync : IDisposable
    {
        /// <summary>
        /// Equivalent to <see cref="IPersistentDataStore.Init(FullDataSet{SerializedItemDescriptor})"/>.
        /// </summary>
        /// <param name="allData">a list of <see cref="DataKind"/> instances and their
        /// corresponding data sets</param>
        Task InitAsync(FullDataSet<SerializedItemDescriptor> allData);

        /// <summary>
        /// Equivalent to <see cref="IPersistentDataStore.Get(DataKind, string)"/>.
        /// </summary>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique key of the item within that collection</param>
        /// <returns>a versioned item that contains the stored data (or placeholder for
        /// deleted data); null if the key is unknown</returns>
        Task<SerializedItemDescriptor?> GetAsync(DataKind kind, string key);

        /// <summary>
        /// Equivalent to <see cref="IPersistentDataStore.GetAll(DataKind)"/>.
        /// </summary>
        /// <param name="kind">specifies which collection to use</param>
        /// <returns>a mapping of string keys to items</returns>
        Task<IEnumerable<KeyValuePair<string, SerializedItemDescriptor>>> GetAllAsync(DataKind kind);

        /// <summary>
        /// Equivalent to <see cref="IPersistentDataStore.Upsert(DataKind, string, SerializedItemDescriptor)"/>.
        /// </summary>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique key for the item within that collection</param>
        /// <param name="item">the item to insert or update</param>
        /// <returns>true if the item was updated; false if it was not updated because the
        /// store contains an equal or greater version</returns>
        Task<bool> UpsertAsync(DataKind kind, string key, SerializedItemDescriptor item);

        /// <summary>
        /// Equivalent to <see cref="IPersistentDataStore.Initialized"/>.
        /// </summary>
        Task<bool> InitializedAsync();
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IPersistentDataStoreAsync"/>.
    /// </summary>
    /// <seealso cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/>
    /// <seealso cref="Components.PersistentStore(IPersistentDataStoreAsyncFactory)"/>
    public interface IPersistentDataStoreAsyncFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <returns>a <see cref="IPersistentDataStoreAsync"/> instance</returns>
        IPersistentDataStoreAsync CreatePersistentDataStore();
    }
}

﻿using System;
using System.Threading.Tasks;

using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Subsystems
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
        /// <returns>a collection of key-value pairs; the ordering is not significant</returns>
        Task<KeyedItems<SerializedItemDescriptor>> GetAllAsync(DataKind kind);

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
        Task<bool> IsStoreAvailableAsync();
    }
}

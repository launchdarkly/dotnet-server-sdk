using System;
using System.Collections.Generic;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface for a data store that holds feature flags and related data received by the streaming client.
    /// </summary>
    /// <seealso cref="IFeatureStoreFactory"/>
    public interface IFeatureStore : IDisposable
    {
        /// <summary>
        /// Retrieve an object from the specified collection, or return null if not found.
        /// </summary>
        /// <typeparam name="T">the returned object class</typeparam>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique string key of the object</param>
        /// <returns>the found object or null</returns>
        T Get<T>(VersionedDataKind<T> kind, string key) where T : class, IVersionedData;

        /// <summary>
        /// Retrieve all objects from the specified collection.
        /// </summary>
        /// <typeparam name="T">the returned object class</typeparam>
        /// <param name="kind">specifies which collection to use</param>
        /// <returns>a dictionary of string keys to objects</returns>
        IDictionary<string, T> All<T>(VersionedDataKind<T> kind) where T : class, IVersionedData;

        /// <summary>
        /// Overwrite the store's contents with a set of objects for each collection.
        /// </summary>
        /// <param name="allData">a dictionary where each key specifies a collection, and each value
        /// is a map of string keys to objects in that collection</param>
        void Init(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData);

        /// <summary>
        /// Delete an object if its version is less than the specified version.
        /// </summary>
        /// <typeparam name="T">the object class</typeparam>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="key">the unique string key of the object</param>
        /// <param name="version">the version number of the deletion; the object will only be deleted if
        /// its existing version is less than this</param>
        void Delete<T>(VersionedDataKind<T> kind, string key, int version) where T : IVersionedData;

        /// <summary>
        /// Update or insert an object in the specified collection. For updates, the object will only be
        /// updated if the existing version is less than the new version.
        /// </summary>
        /// <typeparam name="T">the object class</typeparam>
        /// <param name="kind">specifies which collection to use</param>
        /// <param name="item">the item to insert or update</param>
        void Upsert<T>(VersionedDataKind<T> kind, T item) where T : IVersionedData;

        /// <summary>
        /// Check whether this store has been initialized with any data yet.
        /// </summary>
        /// <returns>true if the store contains data</returns>
        bool Initialized();
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IFeatureStore"/>.
    /// </summary>
    /// <seealso cref="IConfigurationBuilder.FeatureStoreFactory(IFeatureStoreFactory)"/>
    /// <seealso cref="Components"/>
    public interface IFeatureStoreFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <returns>an <c>IStoreEvents</c> instance</returns>
        IFeatureStore CreateFeatureStore();
    }
}

using System;
using System.Collections.Generic;

namespace LaunchDarkly.Client.Utils
{
    /// <summary>
    /// Interface for a simplified subset of the functionality of <see cref="IDataStore"/>, to be
    /// used in conjunction with <see cref="CachingStoreWrapper"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This allows developers of custom <see cref="IDataStore"/> implementations to avoid repeating logic that
    /// would commonly be needed in any such implementation, such as caching. Instead, they can
    /// implement only <see cref="IDataStoreCore"/> and then create a <see cref="CachingStoreWrapper"/>.
    /// </para>
    /// <para>
    /// This interface assumes that your code is synchronous. For asynchronous implementations,
    /// use <see cref="IDataStoreCoreAsync"/> instead.
    /// </para>
    /// <para>
    /// Note that these methods do not take any generic type parameters; all storeable entities are
    /// treated as implementations of the <see cref="IVersionedData"/> interface, and an
    /// <see cref="IVersionedDataKind"/> instance is used to specify what kind of entity is
    /// being referenced. If entities will be marshaled and unmarshaled, this must be done via
    /// reflection, using the type specified by <see cref="IVersionedDataKind.GetItemType"/>.
    /// <see cref="DataStoreHelpers"/> may be useful for this.
    /// </para>
    /// </remarks>
    public interface IDataStoreCore : IDisposable
    {
        /// <summary>
        /// Initializes (or re-initializes) the store with the specified set of objects. Any existing
        /// entries will be removed. Implementations can assume that this set of objects is up to
        /// date; there is no need to perform individual version comparisons between the existing
        /// objects and the supplied data.
        /// </summary>
        /// <param name="allData">all objects to be stored</param>
        void InitInternal(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData);

        /// <summary>
        /// Returns the object to which the specified key is mapped, or null if no such item exists.
        /// The method should not attempt to filter out any items based on their Deleted property,
        /// nor to cache any items.
        /// </summary>
        /// <param name="kind">the kind of objects to get</param>
        /// <param name="key">the key whose associated object is to be returned</param>
        /// <returns>the object to which the specified key is mapped, or null</returns>
        IVersionedData GetInternal(IVersionedDataKind kind, string key);

        /// <summary>
        /// Returns a dictionary of all associated objects of a given kind. The method should not
        /// attempt to filter out any items based on their Deleted property, nor to cache any items.
        /// </summary>
        /// <param name="kind">the kind of objects to get</param>
        /// <returns>a dictionary of all associated objects</returns>
        IDictionary<string, IVersionedData> GetAllInternal(IVersionedDataKind kind);

        /// <summary>
        /// Updates or inserts the object associated with the specified key. If an item with
        /// the same key already exists, it should update it only if the new item's <see cref="IVersionedData.Version"/>
        /// value is greater than the old one. It should return the final state of the item, i.e.
        /// if the update succeeded then it returns the item that was passed in, and if the update
        /// failed due to the version check then it returns the item that is currently in the data
        /// store (this ensures that CachingStoreWrapper will update the cache correctly).
        /// </summary>
        /// <param name="kind">the kind of object to update</param>
        /// <param name="item">the object to update or insert</param>
        /// <returns>the state of the object after the update</returns>
        IVersionedData UpsertInternal(IVersionedDataKind kind, IVersionedData item);

        /// <summary>
        /// Returns true if this store has been initialized. In a shared data store, it should be
        /// able to detect this even if InitInternal was called in a different process, i.e. the
        /// test should be based on looking at what is in the data store. The method does not need
        /// to worry about caching this value; CachingStoreWrapper will only call it when necessary.
        /// </summary>
        /// <returns>true if the store has been initialized</returns>
        bool InitializedInternal();
    }
}

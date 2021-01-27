using LaunchDarkly.Client.Utils;

namespace LaunchDarkly.Client.Interfaces
{
    // Currently we're still using the old-style store interfaces in Utils. In the 6.0 SDK
    // these are defined differently in Interfaces.

    /// <summary>
    /// Interface for a factory that creates some implementation of a persistent data store.
    /// </summary>
    /// <seealso cref="ConfigurationBuilder.DataStore(IFeatureStoreFactory)"/>
    /// <seealso cref="Components.PersistentDataStore(IPersistentDataStoreFactory)"/>
    public interface IPersistentDataStoreFactory
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <returns>the data store instance</returns>
        IFeatureStoreCore CreatePersistentDataStore();
    }

    /// <summary>
    /// Interface for a factory that creates some implementation of a persistent data store.
    /// </summary>
    /// <seealso cref="ConfigurationBuilder.DataStore(IFeatureStoreFactory)"/>
    /// <seealso cref="Components.PersistentDataStore(IPersistentDataStoreAsyncFactory)"/>
    public interface IPersistentDataStoreAsyncFactory
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <returns>the data store instance</returns>
        IFeatureStoreCoreAsync CreatePersistentDataStore();
    }
}

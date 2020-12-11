
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IDataStore"/>.
    /// </summary>
    /// <seealso cref="ConfigurationBuilder.DataStore(IDataStoreFactory)"/>
    /// <seealso cref="Components"/>
    public interface IDataStoreFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="context">configuration of the current client instance</param>
        /// <param name="dataStoreUpdates">the data store can use this object to report
        /// information back to the SDK if desired</param>
        /// <returns>a <see cref="IDataStore"/> instance</returns>
        IDataStore CreateDataStore(LdClientContext context, IDataStoreUpdates dataStoreUpdates);
    }
}

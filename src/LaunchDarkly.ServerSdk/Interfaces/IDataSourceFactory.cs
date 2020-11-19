
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IDataSource"/>.
    /// </summary>
    /// <seealso cref="IConfigurationBuilder.DataSource"/>
    /// <seealso cref="Components"/>
    public interface IDataSourceFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="context">configuration of the current client instance</param>
        /// <param name="dataStoreUpdates">the destination for pushing data updates</param>
        /// <returns>an <see cref="IDataSource"/> instance</returns>
        IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates);
    }
}

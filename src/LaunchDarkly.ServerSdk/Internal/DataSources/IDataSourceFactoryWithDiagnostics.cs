using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    /// <summary>
    /// Internal interface for a factory that creates some implementation of <see cref="IDataSource"/>,
    /// with support for interfacing with a diagnostic store.
    /// </summary>
    /// <seealso cref="IConfigurationBuilder.DataSource"/>
    /// <seealso cref="Components"/>
    internal interface IDataSourceFactoryWithDiagnostics : IDataSourceFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="config">the LaunchDarkly configuration</param>
        /// <param name="dataStore">the store that holds feature flags and related data</param>
        /// <param name="diagnosticStore">the diagnostic store</param>
        /// <returns>an <see cref="IDataSource"/> instance</returns>
        IDataSource CreateDataSource(Configuration config, IDataStore dataStore, IDiagnosticStore diagnosticStore);
    }
}

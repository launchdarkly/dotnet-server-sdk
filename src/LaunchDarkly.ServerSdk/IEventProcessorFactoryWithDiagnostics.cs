using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IEventProcessor"/>,
    /// with support for interfacing with a diagnostic store.
    /// </summary>
    internal interface IEventProcessorFactoryWithDiagnostics : IEventProcessorFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="config">the LaunchDarkly configuration</param>
        /// <param name="diagnosticStore">the diagnostic store</param>
        /// <returns>an <c>IEventProcessor</c> instance</returns>
        IEventProcessor CreateEventProcessor(Configuration config, IDiagnosticStore diagnosticStore);
    }
}

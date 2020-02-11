using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IEventProcessor"/>.
    /// </summary>
    public interface IEventProcessorFactory
    {
        /// <summary>
        /// Creates an implementation instance.
        /// </summary>
        /// <param name="config">the LaunchDarkly configuration</param>
        /// <returns>an <c>IEventProcessor</c> instance</returns>
        IEventProcessor CreateEventProcessor(Configuration config);
    }

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

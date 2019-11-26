using LaunchDarkly.Sdk.Interfaces;

namespace LaunchDarkly.Sdk.Server.Interfaces
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
}

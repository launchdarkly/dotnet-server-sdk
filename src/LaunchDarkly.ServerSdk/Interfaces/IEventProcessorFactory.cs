
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates some implementation of <see cref="IEventProcessor"/>.
    /// </summary>
    public interface IEventProcessorFactory
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <param name="context">configuration of the current client instance</param>
        /// <returns>an <c>IEventProcessor</c> instance</returns>
        IEventProcessor CreateEventProcessor(LdClientContext context);
    }
}

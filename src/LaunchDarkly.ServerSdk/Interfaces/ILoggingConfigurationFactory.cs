
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates an <see cref="ILoggingConfiguration"/>.
    /// </summary>
    public interface ILoggingConfigurationFactory
    {
        /// <summary>
        /// Creates the configuration object.
        /// </summary>
        /// <remarks>
        /// This method is called by the SDK. Application code does not need to use it.
        /// </remarks>
        /// <returns>the logging configuration</returns>
        ILoggingConfiguration CreateLoggingConfiguration();
    }
}

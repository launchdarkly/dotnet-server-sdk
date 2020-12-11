
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates an <see cref="ILoggingConfiguration"/>.
    /// </summary>
    public interface ILoggingConfigurationFactory
    {
        /// <summary>
        /// Creates the configuration object. This is called internally by the SDK.
        /// </summary>
        /// <returns>the logging configuration</returns>
        ILoggingConfiguration CreateLoggingConfiguration();
    }
}


namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates a <see cref="LoggingConfiguration"/>.
    /// </summary>
    public interface ILoggingConfigurationFactory
    {
        /// <summary>
        /// Called internally by the SDK to create a configuration instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <returns>the logging configuration</returns>
        LoggingConfiguration CreateLoggingConfiguration();
    }
}


namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates an <see cref="ILoggingConfiguration"/>.
    /// </summary>
    public interface ILoggingConfigurationFactory
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <returns>the logging configuration</returns>
        ILoggingConfiguration CreateLoggingConfiguration();
    }
}


namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates a <see cref="BigSegmentsConfiguration"/>.
    /// </summary>
    public interface IBigSegmentsConfigurationFactory
    {
        /// <summary>
        /// Called internally by the SDK to create a configuration instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <param name="context">configuration of the current client instance</param>
        /// <returns>a <see cref="BigSegmentsConfiguration"/> instance</returns>
        BigSegmentsConfiguration CreateBigSegmentsConfiguration(LdClientContext context);
    }
}

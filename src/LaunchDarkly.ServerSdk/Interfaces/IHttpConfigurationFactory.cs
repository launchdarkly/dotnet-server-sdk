
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates an <see cref="IHttpConfiguration"/>.
    /// </summary>
    /// <seealso cref="Components.HttpConfiguration"/>
    /// <seealso cref="ConfigurationBuilder.Http(IHttpConfigurationFactory)"/>
    public interface IHttpConfigurationFactory
    {
        /// <summary>
        /// Creates the configuration object. This is called internally by the SDK.
        /// </summary>
        /// <param name="basicConfiguration">provides the basic SDK configuration properties</param>
        /// <returns>an <see cref="IHttpConfiguration"/></returns>
        IHttpConfiguration CreateHttpConfiguration(BasicConfiguration basicConfiguration);
    }
}

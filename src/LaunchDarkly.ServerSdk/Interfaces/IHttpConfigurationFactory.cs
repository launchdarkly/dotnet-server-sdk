
namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Interface for a factory that creates an <see cref="HttpConfiguration"/>.
    /// </summary>
    /// <seealso cref="Components.HttpConfiguration"/>
    /// <seealso cref="ConfigurationBuilder.Http(IHttpConfigurationFactory)"/>
    public interface IHttpConfigurationFactory
    {
        /// <summary>
        /// Called internally by the SDK to create an implementation instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <param name="basicConfiguration">provides the basic SDK configuration properties</param>
        /// <returns>an <see cref="HttpConfiguration"/></returns>
        HttpConfiguration CreateHttpConfiguration(BasicConfiguration basicConfiguration);
    }
}

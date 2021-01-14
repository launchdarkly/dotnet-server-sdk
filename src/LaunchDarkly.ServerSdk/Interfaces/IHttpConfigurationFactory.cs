
namespace LaunchDarkly.Client.Interfaces
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
        /// <param name="config">the SDK configuration</param>
        /// <returns>an <see cref="IHttpConfiguration"/></returns>
        IHttpConfiguration CreateHttpConfiguration(Configuration config);
    }
}

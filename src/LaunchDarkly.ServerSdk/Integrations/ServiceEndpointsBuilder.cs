using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the SDK's service URIs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If you want to set non-default values for any of these properties, create a builder with
    /// <see cref="Components.ServiceEndpoints()"/>, change its properties with the methods of this class, and pass it
    /// to <see cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)" />.
    /// </para>
    /// <para>
    /// The default behavior, if you do not change any of these properties, is that the SDK will connect
    /// to the standard endpoints in the LaunchDarkly production service. There are several use cases for
    /// changing these properties:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// You are using the <a href="https://docs.launchdarkly.com/home/advanced/relay-proxy">LaunchDarkly
    /// Relay Proxy</a>. In this case, set <see cref="RelayProxy(Uri)"/> to the base URI of the Relay Proxy
    /// instance. Note that this is not the same as a regular HTTP proxy, which would be set with
    /// <see cref="HttpConfigurationBuilder.Proxy(System.Net.IWebProxy)"/>.
    /// </description></item>
    /// <item><description>
    /// You are connecting to a private instance of LaunchDarkly, rather than the standard production
    /// services. In this case, there will be custom base URIs for each service, so you must set
    /// <see cref="Streaming(Uri)"/>, <see cref="Polling(Uri)"/>, and <see cref="Events(Uri)"/>.
    /// </description></item>
    /// <item><description>
    /// You are connecting to a test fixture that simulates the service endpoints. In this case, you
    /// may set the base URIs to whatever you want, although the SDK will still set the URI paths to
    /// the expected paths for LaunchDarkly services.
    /// </description></item>
    /// </list>
    /// <para>
    /// Each of the setter methods can be called with either a <see cref="Uri"/> or an equivalent
    /// string. Passing a string that is not a valid URI will cause an immediate
    /// <see cref="UriFormatException"/>
    /// </para>
    /// <para>
    /// If you are using a private instance and you set some of the base URIs, but not all of them,
    /// the SDK will log an error and may not work properly. The only exception is if you have explicitly
    /// disabled the SDK's use of one of the services: for instance, if you have disabled analytics
    /// events with <see cref="Components.NoEvents"/>, you do not have to set <see cref="Events(Uri)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code >
    ///     // Example of specifying a Relay Proxy instance
    ///     var config = Configuration.Builder(sdkKey)
    ///         .ServiceEndpoints(Components.ServiceEndpoints()
    ///             .RelayProxy("http://my-relay-hostname:8080"))
    ///         .Build();
    ///
    ///     // Example of specifying a private LaunchDarkly instance
    ///     var config = Configuration.Builder(sdkKey)
    ///         .ServiceEndpoints(Components.ServiceEndpoints()
    ///             .Streaming("https://stream.mycompany.launchdarkly.com")
    ///             .Polling("https://app.mycompany.launchdarkly.com")
    ///             .Events("https://events.mycompany.launchdarkly.com"))
    ///         .Build();
    /// </code>
    /// </example>
    public class ServiceEndpointsBuilder
    {
        private Uri _streamingBaseUri = null;
        private Uri _pollingBaseUri = null;
        private Uri _eventsBaseUri = null;

        internal ServiceEndpointsBuilder() { }

        internal ServiceEndpointsBuilder(ServiceEndpoints copyFrom)
        {
            _streamingBaseUri = copyFrom.StreamingBaseUri;
            _pollingBaseUri = copyFrom.PollingBaseUri;
            _eventsBaseUri = copyFrom.EventsBaseUri;
        }

        /// <summary>
        /// Sets a custom base URI for the events service.
        /// </summary>
        /// <remarks>
        /// You should only call this method if you are using a private instance or a test fixture
        /// (see <see cref="ServiceEndpointsBuilder"/>). If you are using the LaunchDarkly Relay Proxy,
        /// call <see cref="RelayProxy(Uri)"/> instead.
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .ServiceEndpoints(Components.ServiceEndpoints()
        ///             .Streaming("https://stream.mycompany.launchdarkly.com")
        ///             .Polling("https://app.mycompany.launchdarkly.com")
        ///             .Events("https://events.mycompany.launchdarkly.com"))
        ///         .Build();
        /// </example>
        /// <param name="eventsBaseUri">the base URI of the events service; null to use the default</param>
        /// <returns>the builder</returns>
        /// <seealso cref="Events(string)"/>
        public ServiceEndpointsBuilder Events(Uri eventsBaseUri)
        {
            _eventsBaseUri = eventsBaseUri;
            return this;
        }

        /// <summary>
        /// Equivalent to <see cref="Events(Uri)"/>, specifying the URI as a string.
        /// </summary>
        /// <param name="eventsBaseUri">the base URI of the events service, or
        /// <see langword="null"/> to reset to the default</param>
        /// <returns>the same builder</returns>
        /// <exception cref="UriFormatException">if the string is not null and is not a valid URI</exception>
        /// <seealso cref="Events(Uri)"/>
        public ServiceEndpointsBuilder Events(string eventsBaseUri) =>
            Events(new Uri(eventsBaseUri));

        /// <summary>
        /// Sets a custom base URI for the polling service.
        /// </summary>
        /// <remarks>
        /// You should only call this method if you are using a private instance or a test fixture
        /// (see <see cref="ServiceEndpointsBuilder"/>). If you are using the LaunchDarkly Relay Proxy,
        /// call <see cref="RelayProxy(Uri)"/> instead.
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .ServiceEndpoints(Components.ServiceEndpoints()
        ///             .Streaming("https://stream.mycompany.launchdarkly.com")
        ///             .Polling("https://app.mycompany.launchdarkly.com")
        ///             .Events("https://events.mycompany.launchdarkly.com"))
        ///         .Build();
        /// </example>
        /// <param name="pollingBaseUri">the base URI of the polling service; null to use the default</param>
        /// <returns>the builder</returns>
        /// <seealso cref="Polling(string)"/>
        public ServiceEndpointsBuilder Polling(Uri pollingBaseUri)
        {
            _pollingBaseUri = pollingBaseUri;
            return this;
        }

        /// <summary>
        /// Equivalent to <see cref="Polling(Uri)"/>, specifying the URI as a string.
        /// </summary>
        /// <param name="pollingBaseUri">the base URI of the polling service, or
        /// <see langword="null"/> to reset to the default</param>
        /// <returns>the same builder</returns>
        /// <exception cref="UriFormatException">if the string is not null and is not a valid URI</exception>
        /// <seealso cref="Polling(Uri)"/>
        public ServiceEndpointsBuilder Polling(string pollingBaseUri) =>
            Polling(new Uri(pollingBaseUri));

        /// <summary>
        /// Specifies a single base URI for a Relay Proxy instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When using the <see href="https://docs.launchdarkly.com/home/relay-proxy">LaunchDarkly Relay Proxy</see>,
        /// the SDK only needs to know the single base URI of the Relay Proxy, which will provide all of the
        /// proxied service endpoints.
        /// </para>
        /// <para>
        /// Note that this is not the same as a regular HTTP proxy, which would be set with
        /// <see cref="HttpConfigurationBuilder.Proxy(System.Net.IWebProxy)"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     var relayUri = new Uri("http://my-relay-hostname:8080");
        ///     var config = Configuration.Builder(sdkKey)
        ///         .ServiceEndpoints(Components.ServiceEndpoints().RelayProxy(relayUri))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="relayProxyBaseUri">the Relay Proxy base URI, or
        /// <see langword="null"/> to reset to default endpoints</param>
        /// <returns>the builder</returns>
        /// <seealso cref="RelayProxy(string)"/>
        public ServiceEndpointsBuilder RelayProxy(Uri relayProxyBaseUri)
        {
            _streamingBaseUri = relayProxyBaseUri;
            _pollingBaseUri = relayProxyBaseUri;
            _eventsBaseUri = relayProxyBaseUri;
            return this;
        }

        /// <summary>
        /// Equivalent to <see cref="RelayProxy(Uri)"/>, specifying the URI as a string.
        /// </summary>
        /// <param name="relayProxyBaseUri">the Relay Proxy base URI, or
        /// <see langword="null"/> to reset to default endpoints</param>
        /// <returns>the same builder</returns>
        /// <exception cref="UriFormatException">if the string is not null and is not a valid URI</exception>
        /// <seealso cref="RelayProxy(Uri)"/>
        public ServiceEndpointsBuilder RelayProxy(string relayProxyBaseUri) =>
            RelayProxy(new Uri(relayProxyBaseUri));

        /// <summary>
        /// Sets a custom base URI for the streaming service.
        /// </summary>
        /// <remarks>
        /// You should only call this method if you are using a private instance or a test fixture
        /// (see <see cref="ServiceEndpointsBuilder"/>). If you are using the LaunchDarkly Relay Proxy,
        /// call <see cref="RelayProxy(Uri)"/> instead.
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .ServiceEndpoints(Components.ServiceEndpoints()
        ///             .Streaming("https://stream.mycompany.launchdarkly.com")
        ///             .Polling("https://app.mycompany.launchdarkly.com")
        ///             .Events("https://events.mycompany.launchdarkly.com"))
        ///         .Build();
        /// </example>
        /// <param name="streamingBaseUri">the base URI of the streaming service; null to use the default</param>
        /// <returns>the builder</returns>
        /// <seealso cref="Streaming(string)"/>
        public ServiceEndpointsBuilder Streaming(Uri streamingBaseUri)
        {
            _streamingBaseUri = streamingBaseUri;
            return this;
        }

        /// <summary>
        /// Equivalent to <see cref="Streaming(Uri)"/>, specifying the URI as a string.
        /// </summary>
        /// <param name="streamingBaseUri">the base URI of the streaming service, or
        /// <see langword="null"/> to reset to the default</param>
        /// <returns>the same builder</returns>
        /// <exception cref="UriFormatException">if the string is not null and is not a valid URI</exception>
        /// <seealso cref="Streaming(Uri)"/>
        public ServiceEndpointsBuilder Streaming(string streamingBaseUri) =>
            Streaming(new Uri(streamingBaseUri));

        /// <summary>
        /// Called internally by the SDK to create a configuration instance. Applications do not need
        /// to call this method.
        /// </summary>
        /// <returns>the configuration object</returns>
        public ServiceEndpoints Build()
        {
            // The logic here is based on the assumption that if *any* custom URIs have been set,
            // then we do not want to use default values for any that were not set, so we will leave
            // those null. That way, if we decide later on (in other component factories, such as
            // EventProcessorBuilder) that we are actually interested in one of these values, and we
            // see that it is null, we can assume that there was a configuration mistake and log an
            // error.
            if (_streamingBaseUri is null && _pollingBaseUri is null && _eventsBaseUri is null)
            {
                return StandardEndpoints.BaseUris;
            }
            return new ServiceEndpoints(_streamingBaseUri, _pollingBaseUri, _eventsBaseUri);
        }
    }
}

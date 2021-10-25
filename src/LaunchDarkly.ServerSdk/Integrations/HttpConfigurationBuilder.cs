using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Internal.Events.DiagnosticConfigProperties;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the SDK's networking behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If you want to set non-default values for any of these properties, create a builder with
    /// <see cref="Components.HttpConfiguration"/>, change its properties with the methods of this class, and
    /// pass it to <see cref="ConfigurationBuilder.Http(IHttpConfigurationFactory)"/>:
    /// </para>
    /// <example>
    /// <code>
    ///     var config = Configuration.Builder(sdkKey)
    ///         .Http(
    ///             Components.HttpConfiguration()
    ///                 .ConnectTimeout(TimeSpan.FromMilliseconds(3000))
    ///         )
    ///         .Build();
    /// </code>
    /// </example>
    /// </remarks>
    public sealed class HttpConfigurationBuilder : IHttpConfigurationFactory, IDiagnosticDescription
    {
        /// <summary>
        /// The default value for <see cref="ConnectTimeout(TimeSpan)"/>: two seconds.
        /// </summary>
        public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The default value for <see cref="ReadTimeout(TimeSpan)"/>: 10 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The default value for <see cref="ResponseStartTimeout(TimeSpan)"/>: 10 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultResponseStartTimeout = TimeSpan.FromSeconds(10);

        internal TimeSpan _connectTimeout = DefaultConnectTimeout;
        internal List<KeyValuePair<string, string>> _customHeaders = new List<KeyValuePair<string, string>>();
        internal HttpMessageHandler _messageHandler = null;
        internal IWebProxy _proxy = null;
        internal TimeSpan _readTimeout = DefaultReadTimeout;
        internal TimeSpan _responseStartTimeout = DefaultResponseStartTimeout;
        internal string _wrapperName = null;
        internal string _wrapperVersion = null;

        /// <summary>
        /// Sets the network connection timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the time allowed for the underlying HTTP client to connect to the
        /// LaunchDarkly server, for any individual network connection.
        /// </para>
        /// <para>
        /// It is not the same as <see cref="ConfigurationBuilder.StartWaitTime(TimeSpan)"/>, which
        /// limits the time for initializing the SDK regardless of how many individual HTTP requests
        /// are done in that time.
        /// </para>
        /// <para>
        /// Not all .NET platforms support setting a connection timeout. It is implemented as
        /// a property of <c>System.Net.Http.SocketsHttpHandler</c> in .NET Core 2.1+ and .NET
        /// 5+, but is unavailable in .NET Framework and .NET Standard. On platforms where it
        /// is not supported, only <see cref="ResponseStartTimeout"/> will be used.
        /// </para>
        /// <para>
        /// Also, since this is implemented only in <c>SocketsHttpHandler</c>, if you have
        /// specified some other HTTP handler implementation with <see cref="HttpMessageHandler"/>,
        /// the <see cref="ConnectTimeout"/> here will be ignored.
        /// </para>
        /// </remarks>
        /// <param name="connectTimeout">the timeout</param>
        /// <returns>the builder</returns>
        /// <seealso cref="ResponseStartTimeout"/>
        public HttpConfigurationBuilder ConnectTimeout(TimeSpan connectTimeout)
        {
            _connectTimeout = connectTimeout;
            return this;
        }

        /// <summary>
        /// Specifies a custom HTTP header that should be added to all SDK requests.
        /// </summary>
        /// <remarks>
        /// This may be helpful if you are using a gateway or proxy server that requires a specific header in
        /// requests. You may add any number of headers.
        /// </remarks>
        /// <param name="name">the header name</param>
        /// <param name="value">the header value</param>
        /// <returns>the builder</returns>
        public HttpConfigurationBuilder CustomHeader(string name, string value)
        {
            _customHeaders.Add(new KeyValuePair<string, string>(name, value));
            return this;
        }

        /// <summary>
        /// Specifies a custom HTTP message handler implementation.
        /// </summary>
        /// <remarks>
        /// This is mainly useful for testing, to cause the SDK to use custom logic instead of actual HTTP requests,
        /// but can also be used to customize HTTP behavior on platforms where .NET's default handler is not optimal.
        /// </remarks>
        /// <param name="messageHandler">the message handler, or null to use the platform's default handler</param>
        /// <returns>the builder</returns>
        public HttpConfigurationBuilder MessageHandler(HttpMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
            return this;
        }

        /// <summary>
        /// Sets an HTTP proxy for making connections to LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is ignored if you have specified a custom message handler with <see cref="MessageHandler(HttpMessageHandler)"/>,
        /// since proxy behavior is implemented by the message handler.
        /// </para>
        /// <para>
        /// Note that this is not the same as the <see href="https://docs.launchdarkly.com/home/relay-proxy">LaunchDarkly
        /// Relay Proxy</see>, which would be set with
        /// <see cref="ServiceEndpointsBuilder.RelayProxy(Uri)"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     // Example of using an HTTP proxy with basic authentication
        ///     
        ///     var proxyUri = new Uri("http://my-proxy-host:8080");
        ///     var proxy = new System.Net.WebProxy(proxyUri);
        ///     var credentials = new System.Net.CredentialCache();
        ///     credentials.Add(proxyUri, "Basic",
        ///         new System.Net.NetworkCredential("username", "password"));
        ///     proxy.Credentials = credentials;
        ///     
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Http(
        ///             Components.HttpConfiguration().Proxy(proxy)
        ///         )
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="proxy">any implementation of <c>System.Net.IWebProxy</c></param>
        /// <returns>the builder</returns>
        public HttpConfigurationBuilder Proxy(IWebProxy proxy)
        {
            _proxy = proxy;
            return this;
        }

        /// <summary>
        /// Sets the socket read timeout.
        /// </summary>
        /// <remarks>
        /// Sets the socket timeout. This is the amount of time without receiving data on a connection that the
        /// SDK will tolerate before signaling an error. This does <i>not</i> apply to the streaming connection
        /// used by <see cref="Components.StreamingDataSource"/>, which has its own non-configurable read timeout
        /// based on the expected behavior of the LaunchDarkly streaming service.
        /// </remarks>
        /// <param name="readTimeout">the socket read timeout</param>
        /// <returns>the builder</returns>
        public HttpConfigurationBuilder ReadTimeout(TimeSpan readTimeout)
        {
            _readTimeout = readTimeout;
            return this;
        }

        /// <summary>
        /// Sets the maximum amount of time to wait for the beginning of an HTTP response.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This limits how long the SDK will wait from the time it begins trying to make a
        /// network connection for an individual HTTP request to the time it starts receiving
        /// any data from the server. It is equivalent to the <c>Timeout</c> property in
        /// <c>HttpClient</c>.
        /// </para>
        /// <para>
        /// It is not the same as <see cref="ConfigurationBuilder.StartWaitTime(TimeSpan)"/>, which
        /// limits the time for initializing the SDK regardless of how many individual HTTP requests
        /// are done in that time.
        /// </para>
        /// </remarks>
        /// <param name="responseStartTimeout">the timeout</param>
        /// <returns>the builder</returns>
        /// <seealso cref="ConnectTimeout"/>
        public HttpConfigurationBuilder ResponseStartTimeout(TimeSpan responseStartTimeout)
        {
            _responseStartTimeout = responseStartTimeout;
            return this;
        }

        /// <summary>
        /// For use by wrapper libraries to set an identifying name for the wrapper being used.
        /// </summary>
        /// <remarks>
        /// This will be included in a header during requests to the LaunchDarkly servers to allow recording
        /// metrics on the usage of these wrapper libraries.
        /// </remarks>
        /// <param name="wrapperName">an identifying name for the wrapper library</param>
        /// <param name="wrapperVersion">version string for the wrapper library</param>
        /// <returns>the builder</returns>
        public HttpConfigurationBuilder Wrapper(string wrapperName, string wrapperVersion)
        {
            _wrapperName = wrapperName;
            _wrapperVersion = wrapperVersion;
            return this;
        }

        /// <inheritdoc/>
        public HttpConfiguration CreateHttpConfiguration(BasicConfiguration basicConfiguration)
        {
            var httpProperties = MakeHttpProperties(basicConfiguration);
            return new HttpConfiguration(
                httpProperties,
                _messageHandler,
                _responseStartTimeout
                );
        }

        private HttpProperties MakeHttpProperties(BasicConfiguration basicConfiguration)
        {
            var httpProperties = HttpProperties.Default
                .WithAuthorizationKey(basicConfiguration.SdkKey)
                .WithConnectTimeout(_connectTimeout)
                .WithHttpMessageHandlerFactory(_messageHandler is null ?
                    (Func<HttpProperties, HttpMessageHandler>)null :
                    _ => _messageHandler)
                .WithProxy(_proxy)
                .WithReadTimeout(_readTimeout)
                .WithUserAgent("DotNetClient/" + AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)))
                .WithWrapper(_wrapperName, _wrapperVersion);

            foreach (var kv in _customHeaders)
            {
                httpProperties = httpProperties.WithHeader(kv.Key, kv.Value);
            }

            return httpProperties;
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic) =>
            LdValue.BuildObject()
                .WithHttpProperties(MakeHttpProperties(basic))
                .Build();
    }
}

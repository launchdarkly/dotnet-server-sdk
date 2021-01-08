using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Interfaces;

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

        internal TimeSpan _connectTimeout = DefaultConnectTimeout;
        internal List<KeyValuePair<string, string>> _customHeaders = new List<KeyValuePair<string, string>>();
        internal HttpMessageHandler _messageHandler = null;
        internal IWebProxy _proxy = null;
        internal TimeSpan _readTimeout = DefaultReadTimeout;
        internal string _wrapperName = null;
        internal string _wrapperVersion = null;

        /// <summary>
        /// Sets the connection timeout.
        /// </summary>
        /// <remarks>
        /// This is the time allowed for the SDK to make a socket connection to any of the LaunchDarkly services.
        /// </remarks>
        /// <param name="connectTimeout">the connection timeout</param>
        /// <returns>the builder</returns>
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
        /// This is ignored if you have specified a custom message handler with <see cref="MessageHandler(HttpMessageHandler)"/>,
        /// since proxy behavior is implemented by the message handler.
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
        public IHttpConfiguration CreateHttpConfiguration(BasicConfiguration basicConfiguration) =>
            new HttpConfigurationImpl(this, basicConfiguration.SdkKey);

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic) =>
            LdValue.BuildObject()
                .Add("connectTimeoutMillis", _connectTimeout.TotalMilliseconds)
                .Add("socketTimeoutMillis", _readTimeout.TotalMilliseconds)
                .Add("usingProxy", DetectProxy())
                .Add("usingProxyAuthenticator", DetectProxyAuth())
                .Build();

        // DetectProxy and DetectProxyAuth do not cover every mechanism that could be used to configure
        // a proxy; for instance, there is HttpClient.DefaultProxy, which only exists in .NET Core 3.x and
        // .NET 5.x. But since we're only trying to gather diagnostic stats, this doesn't have to be perfect.
        private bool DetectProxy() =>
            _proxy != null ||
            !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("HTTP_PROXY")) ||
            !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("HTTPS_PROXY")) ||
            !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ALL_PROXY"));

        private bool DetectProxyAuth() =>
            _proxy is WebProxy wp &&
            (wp.Credentials != null || wp.UseDefaultCredentials);

        internal sealed class HttpConfigurationImpl : IHttpConfiguration
        {
            private readonly HttpProperties _httpProperties;

            internal HttpProperties HttpProperties => _httpProperties;

            public TimeSpan ConnectTimeout { get; }
            public IEnumerable<KeyValuePair<string, string>> CustomHeaders { get; }
            public HttpMessageHandler MessageHandler { get; }
            public IWebProxy Proxy { get; }
            public TimeSpan ReadTimeout { get; }
            public IEnumerable<KeyValuePair<string, string>> DefaultHeaders => _httpProperties.BaseHeaders;

            internal HttpConfigurationImpl(HttpConfigurationBuilder builder, string sdkKey)
            {
                ConnectTimeout = builder._connectTimeout;
                CustomHeaders = builder._customHeaders.ToImmutableList();
                MessageHandler = builder._messageHandler;
                Proxy = builder._proxy;
                ReadTimeout = builder._readTimeout;

                var httpProperties = HttpProperties.Default
                    .WithAuthorizationKey(sdkKey)
                    .WithConnectTimeout(builder._connectTimeout)
                    .WithHttpMessageHandlerFactory(MessageHandler is null ?
                        (Func<HttpProperties, HttpMessageHandler>)null :
                        _ => MessageHandler)
                    .WithProxy(builder._proxy)
                    .WithReadTimeout(builder._readTimeout)
                    .WithUserAgent("DotNetClient/" + AssemblyVersions.GetAssemblyVersionStringForType(typeof(LdClient)))
                    .WithWrapper(builder._wrapperName, builder._wrapperVersion);

                foreach (var kv in builder._customHeaders)
                {
                    httpProperties = httpProperties.WithHeader(kv.Key, kv.Value);
                }

                _httpProperties = httpProperties;
            }
        }
    }
}

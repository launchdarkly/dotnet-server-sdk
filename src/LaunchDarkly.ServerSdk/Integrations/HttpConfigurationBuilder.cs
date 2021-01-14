using System;
using System.Collections.Generic;
using System.Net.Http;
using LaunchDarkly.Client;
using LaunchDarkly.Client.Interfaces;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client.Integrations
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
        internal HttpMessageHandler _messageHandler = new HttpClientHandler();
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
        public IHttpConfiguration CreateHttpConfiguration(Configuration config)
        {
            return new HttpConfigurationImpl
            {
                ConnectTimeout = _connectTimeout,
                MessageHandler = _messageHandler,
                ReadTimeout = _readTimeout,
                WrapperName = _wrapperName,
                WrapperVersion = _wrapperVersion
            };
        }

        /// <summary>
        /// Used internally by the SDK to inspect the configuration.
        /// </summary>
        /// <param name="config">the current configuration</param>
        /// <returns>diagnostic properties</returns>
        public LdValue DescribeConfiguration(Configuration config)
        {
            return LdValue.BuildObject()
                .Add("connectTimeoutMillis", _connectTimeout.TotalMilliseconds)
                .Add("socketTimeoutMillis", _readTimeout.TotalMilliseconds)
                .Add("usingProxy", false)
                .Add("usingProxyAuthenticator", false)
                .Build();
        }

        internal sealed class HttpConfigurationImpl : IHttpConfiguration, IHttpConfigurationInternal
        {
            public TimeSpan ConnectTimeout { get; internal set; }
            public HttpMessageHandler MessageHandler { get; internal set; }
            public TimeSpan ReadTimeout { get; internal set; }
            public string WrapperName { get; internal set; }
            public string WrapperVersion { get; internal set; }
        }
    }
}
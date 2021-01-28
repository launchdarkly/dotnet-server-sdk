using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using LaunchDarkly.Sdk.Internal.Http;
using LaunchDarkly.Sdk.Server.Integrations;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Encapsulates top-level HTTP configuration that applies to all SDK components.
    /// </summary>
    /// <remarks>
    /// Use <see cref="HttpConfigurationBuilder"/> to construct an instance.
    /// </remarks>
    public sealed class HttpConfiguration
    {
        /// <summary>
        /// The network connection timeout.
        /// </summary>
        /// <remarks>
        /// This is the time allowed for the underlying HTTP client to connect to the
        /// LaunchDarkly server, for any individual network connection.
        /// </remarks>
        public TimeSpan ConnectTimeout { get; }

        /// <summary>
        /// HTTP headers to be added to all HTTP requests made by the SDK.
        /// </summary>
        /// <remarks>
        /// These include <c>Authorization</c>, <c>User-Agent</c>, and any headers that were
        /// specified with <see cref="HttpConfigurationBuilder.CustomHeader(string, string)"/>.
        /// </remarks>
        public IEnumerable<KeyValuePair<string, string>> DefaultHeaders { get; }

        /// <summary>
        /// A custom handler for HTTP requests, or null to use the platform's default handler.
        /// </summary>
        public HttpMessageHandler MessageHandler { get; }

        /// <summary>
        /// The proxy configuration, if any.
        /// </summary>
        /// <remarks>
        /// This is only present if a proxy was specified programmatically with
        /// <see cref="HttpConfigurationBuilder.Proxy(IWebProxy)"/>, not if it was
        /// specified with an environment variable.
        /// </remarks>
        public IWebProxy Proxy { get; }

        /// <summary>
        /// The network read timeout (socket timeout).
        /// </summary>
        /// <remarks>
        /// This is the amount of time without receiving data on a connection that the
        /// SDK will tolerate before signaling an error. This does <i>not</i> apply to
        /// the streaming connection used by <see cref="Components.StreamingDataSource"/>,
        /// which has its own non-configurable read timeout based on the expected behavior
        /// of the LaunchDarkly streaming service.
        /// </remarks>
        public TimeSpan ReadTimeout { get; }

        /// <summary>
        /// Used internally by SDK code that uses the HttpProperties abstraction from LaunchDarkly.InternalSdk.
        /// </summary>
        internal HttpProperties HttpProperties { get; }

        /// <summary>
        /// Constructs an instance, setting all properties.
        /// </summary>
        /// <param name="connectTimeout">value for <see cref="ConnectTimeout"/></param>
        /// <param name="defaultHeaders">value for <see cref="DefaultHeaders"/></param>
        /// <param name="messageHandler">value for <see cref="MessageHandler"/></param>
        /// <param name="proxy">value for <see cref="Proxy"/></param>
        /// <param name="readTimeout">value for <see cref="ReadTimeout"/></param>
        public HttpConfiguration(
            TimeSpan connectTimeout,
            IEnumerable<KeyValuePair<string, string>> defaultHeaders,
            HttpMessageHandler messageHandler,
            IWebProxy proxy,
            TimeSpan readTimeout
            ) :
            this(
                MakeHttpProperties(connectTimeout, defaultHeaders, messageHandler, readTimeout),
                messageHandler
                ) { }

        internal HttpConfiguration(
            HttpProperties httpProperties,
            HttpMessageHandler messageHandler
            )
        {
            HttpProperties = httpProperties;
            ConnectTimeout = httpProperties.ConnectTimeout;
            DefaultHeaders = httpProperties.BaseHeaders;
            MessageHandler = messageHandler;
            Proxy = httpProperties.Proxy;
            ReadTimeout = httpProperties.ReadTimeout;
        }

        /// <summary>
        /// Helper method for creating an HTTP client instance using the configured properties.
        /// </summary>
        /// <returns>a client instance</returns>
        public HttpClient NewHttpClient()
        {
            var httpClient = MessageHandler is null ?
                new HttpClient() :
                new HttpClient(MessageHandler, false);
            foreach (var h in DefaultHeaders)
            {
                httpClient.DefaultRequestHeaders.Add(h.Key, h.Value);
            }
            return httpClient;
        }

        internal static HttpProperties MakeHttpProperties(
            TimeSpan connectTimeout,
            IEnumerable<KeyValuePair<string, string>> defaultHeaders,
            HttpMessageHandler messageHandler,
            TimeSpan readTimeout
            )
        {
            var ret = HttpProperties.Default
                .WithConnectTimeout(connectTimeout)
                .WithReadTimeout(readTimeout)
                .WithHttpMessageHandlerFactory(messageHandler is null ?
                    (Func<HttpProperties, HttpMessageHandler>)null :
                    _ => messageHandler);
            foreach (var kv in defaultHeaders)
            {
                ret = ret.WithHeader(kv.Key, kv.Value);
            }
            return ret;
        }
    }
}

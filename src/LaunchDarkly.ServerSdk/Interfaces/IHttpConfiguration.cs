using System;
using System.Collections.Generic;
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
    public interface IHttpConfiguration
    {
        /// <summary>
        /// The network connection timeout.
        /// </summary>
        /// <remarks>
        /// This is the time allowed for the underlying HTTP client to connect to the
        /// LaunchDarkly server, for any individual network connection.
        /// </remarks>
        TimeSpan ConnectTimeout { get; }

        /// <summary>
        /// A custom handler for HTTP requests, or null to use the platform's default handler.
        /// </summary>
        HttpMessageHandler MessageHandler { get; }

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
        TimeSpan ReadTimeout { get; }

        /// <summary>
        /// HTTP headers to be added to all HTTP requests made by the SDK.
        /// </summary>
        /// <remarks>
        /// These include <c>Authorization</c> and <c>User-Agent</c>.
        /// </remarks>
        IEnumerable<KeyValuePair<string, string>> DefaultHeaders { get; }
    }

    /// <summary>
    /// Helper methods for SDK components to use an HTTP configuration.
    /// </summary>
    public static class IHttpConfigurationExtensions
    {
        /// <summary>
        /// Helper method for creating an HTTP client instance using the configured properties.
        /// </summary>
        /// <param name="httpConfig">the HTTP configuration</param>
        /// <returns>a client instance</returns>
        public static HttpClient NewHttpClient(this IHttpConfiguration httpConfig)
        {
            var httpClient = httpConfig.MessageHandler is null ?
                new HttpClient() :
                new HttpClient(httpConfig.MessageHandler, false);
            foreach (var h in httpConfig.DefaultHeaders)
            {
                httpClient.DefaultRequestHeaders.Add(h.Key, h.Value);
            }
            return httpClient;
        }

        /// <summary>
        /// Internal helper method for converting this configuration to the <see cref="HttpProperties"/>
        /// type used by <c>LaunchDarkly.InternalSdk</c>.
        /// </summary>
        /// <param name="httpConfig">the HTTP configuration</param>
        /// <returns>the equivalent <c>HttpProperties</c></returns>
        internal static HttpProperties ToHttpProperties(this IHttpConfiguration httpConfig)
        {
            if (httpConfig is HttpConfigurationBuilder.HttpConfigurationImpl impl)
            {
                return impl.HttpProperties;
            }
            var ret = HttpProperties.Default
                .WithConnectTimeout(httpConfig.ConnectTimeout)
                .WithReadTimeout(httpConfig.ReadTimeout)
                .WithHttpMessageHandler(httpConfig.MessageHandler);
            foreach (var kv in httpConfig.DefaultHeaders)
            {
                ret = ret.WithHeader(kv.Key, kv.Value);
            }
            return ret;
        }
    }
}

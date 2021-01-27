using System;
using System.Net.Http;
using LaunchDarkly.Client.Integrations;

namespace LaunchDarkly.Client.Interfaces
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
    }

    // These properties are defined in an internal interface because otherwise they would have to be
    // immediately obsolete - the 6.0 API has a different way of representing them.
    internal interface IHttpConfigurationInternal : IHttpConfiguration
    {
        string WrapperName { get; }
        string WrapperVersion { get; }
    }
}
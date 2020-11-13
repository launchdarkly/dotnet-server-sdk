using System;
using LaunchDarkly.Sdk.Internal.Stream;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataSources;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the streaming data source.
    /// </summary>
    /// <remarks>
    /// By default, the SDK uses a streaming connection to receive feature flag data from LaunchDarkly. If you want
    /// to customize the behavior of the connection, create a builder with <see cref="Components.StreamingDataSource"/>,
    /// change its properties with the methods of this class, and pass it to
    /// <see cref="IConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
    /// </remarks>
    public class StreamingDataSourceBuilder : IDataSourceFactory, IDiagnosticDescription
    {
        internal static readonly Uri DefaultBaseUri = new Uri("https://stream.launchdarkly.com");

        /// <summary>
        /// The default value for <see cref="InitialReconnectDelay(TimeSpan)"/>: 1000 milliseconds.
        /// </summary>
        public static readonly TimeSpan DefaultInitialReconnectDelay = TimeSpan.FromSeconds(1);

        internal Uri _baseUri = DefaultBaseUri;
        internal TimeSpan _initialReconnectDelay = DefaultInitialReconnectDelay;
        internal StreamManager.EventSourceCreator _eventSourceCreator = null;

        /// <summary>
        /// Sets a custom base URI for the streaming service.
        /// </summary>
        /// <remarks>
        /// You will only need to change this value in the following cases:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// You are using the <a href="https://docs.launchdarkly.com/docs/the-relay-proxy">Relay Proxy</a>.
        /// Set <c>BaseUri</c> to the base URI of the Relay Proxy instance.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// You are connecting to a test server or a nonstandard endpoint for the LaunchDarkly service.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <param name="baseUri">the base URI of the streaming service; null to use the default</param>
        /// <returns>the builder</returns>
        public StreamingDataSourceBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri ?? DefaultBaseUri;
            return this;
        }

        /// <summary>
        /// Sets the initial reconnect delay for the streaming connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The streaming service uses a backoff algorithm (with jitter) every time the connection needs
        /// to be reestablished.The delay for the first reconnection will start near this value, and then
        /// increase exponentially for any subsequent connection failures.
        /// </para>
        /// <para>
        /// The default value is <see cref="DefaultInitialReconnectDelay"/>.
        /// </para>
        /// </remarks>
        /// <param name="initialReconnectDelay">the reconnect time base value</param>
        /// <returns>the builder</returns>
        public StreamingDataSourceBuilder InitialReconnectDelay(TimeSpan initialReconnectDelay)
        {
            _initialReconnectDelay = initialReconnectDelay;
            return this;
        }

        // Exposed for testing
        internal StreamingDataSourceBuilder EventSourceCreator(StreamManager.EventSourceCreator eventSourceCreator)
        {
            _eventSourceCreator = eventSourceCreator;
            return this;
        }

        /// <inheritdoc/>
        public IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
        {
            return new StreamProcessor(
                context,
                dataStoreUpdates,
                _baseUri ?? DefaultBaseUri,
                _initialReconnectDelay,
                _eventSourceCreator
                );
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic)
        {
            return LdValue.BuildObject()
                .Add("streamingDisabled", false)
                .Add("customBaseURI", false)
                .Add("customStreamURI",
                    !(_baseUri ?? DefaultBaseUri).Equals(DefaultBaseUri))
                .Add("reconnectTimeMillis", _initialReconnectDelay.TotalMilliseconds)
                .Add("usingRelayDaemon", false)
                .Build();
        }
    }
}

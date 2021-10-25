using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataSources;

using static LaunchDarkly.Sdk.Internal.Events.DiagnosticConfigProperties;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the streaming data source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, the SDK uses a streaming connection to receive feature flag data from LaunchDarkly. If you want
    /// to customize the behavior of the connection, create a builder with <see cref="Components.StreamingDataSource"/>,
    /// change its properties with the methods of this class, and pass it to
    /// <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
    /// </para>
    /// <para>
    /// Setting <see cref="ConfigurationBuilder.Offline(bool)"/> to <see langword="true"/> will supersede this
    /// setting and completely disable network requests.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    ///     var config = Configuration.Builder(sdkKey)
    ///         .DataSource(Components.PollingDataSource()
    ///             .PollInterval(TimeSpan.FromSeconds(45)))
    ///         .Build();
    /// </code>
    /// </example>
    public sealed class StreamingDataSourceBuilder : IDataSourceFactory, IDiagnosticDescription
    {
        /// <summary>
        /// The default value for <see cref="InitialReconnectDelay(TimeSpan)"/>: 1000 milliseconds.
        /// </summary>
        public static readonly TimeSpan DefaultInitialReconnectDelay = TimeSpan.FromSeconds(1);

        internal Uri _baseUri;
        internal TimeSpan _initialReconnectDelay = DefaultInitialReconnectDelay;
        internal StreamProcessor.EventSourceCreator _eventSourceCreator = null;

        /// <summary>
        /// Deprecated method for setting a custom base URI for the streaming service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The preferred way to set this option is now with
        /// <see cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)"/>. If you set
        /// this deprecated option, it overrides any value that was set with
        /// <see cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)"/>.
        /// </para>
        /// <para>
        /// You will only need to change this value in the following cases:
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// You are using the <a href="https://docs.launchdarkly.com/home/relay-proxy">Relay Proxy</a>.
        /// Set <c>BaseUri</c> to the base URI of the Relay Proxy instance.
        /// </description></item>
        /// <item><description>
        /// You are connecting to a test server or a nonstandard endpoint for the LaunchDarkly service.
        /// </description></item>
        /// </list>
        /// </remarks>
        /// <param name="baseUri">the base URI of the streaming service; null to use the default</param>
        /// <returns>the builder</returns>
        /// <seealso cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)"/>
        [Obsolete("Use ConfigurationBuilder.ServiceEndpoints instead")]
        public StreamingDataSourceBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
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
        internal StreamingDataSourceBuilder EventSourceCreator(StreamProcessor.EventSourceCreator eventSourceCreator)
        {
            _eventSourceCreator = eventSourceCreator;
            return this;
        }

        /// <inheritdoc/>
        public IDataSource CreateDataSource(LdClientContext context, IDataSourceUpdates dataSourceUpdates)
        {
            var configuredBaseUri = _baseUri ??
                StandardEndpoints.SelectBaseUri(context.Basic.ServiceEndpoints, e => e.StreamingBaseUri, "Streaming", context.Basic.Logger);
            return new StreamProcessor(
                context,
                dataSourceUpdates,
                configuredBaseUri,
                _initialReconnectDelay,
                _eventSourceCreator
                );
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic) =>
            LdValue.BuildObject()
            .WithStreamingProperties(
                StandardEndpoints.IsCustomUri(basic.ServiceEndpoints, _baseUri, e => e.StreamingBaseUri),
                false,
                _initialReconnectDelay
                )
            .Set("usingRelayDaemon", false)
            .Build();
    }
}

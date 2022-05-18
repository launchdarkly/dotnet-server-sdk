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
    /// change its properties with the methods of this class, and pass it to <see cref="ConfigurationBuilder.DataSource"/>.
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
    public sealed class StreamingDataSourceBuilder : IComponentConfiguration<IDataSource>, IDiagnosticDescription
    {
        /// <summary>
        /// The default value for <see cref="InitialReconnectDelay(TimeSpan)"/>: 1000 milliseconds.
        /// </summary>
        public static readonly TimeSpan DefaultInitialReconnectDelay = TimeSpan.FromSeconds(1);

        internal TimeSpan _initialReconnectDelay = DefaultInitialReconnectDelay;

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

        /// <inheritdoc/>
        public IDataSource Build(LdClientContext context)
        {
            var configuredBaseUri = StandardEndpoints.SelectBaseUri(
                context.ServiceEndpoints, e => e.StreamingBaseUri, "Streaming",
                context.Logger);
            return new StreamingDataSource(
                context,
                context.DataSourceUpdates,
                configuredBaseUri,
                _initialReconnectDelay
                );
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(LdClientContext context) =>
            LdValue.BuildObject()
            .WithStreamingProperties(
                StandardEndpoints.IsCustomUri(context.ServiceEndpoints, e => e.StreamingBaseUri),
                false,
                _initialReconnectDelay
                )
            .Set("usingRelayDaemon", false)
            .Build();
    }
}

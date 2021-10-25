using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataSources;

using static LaunchDarkly.Sdk.Internal.Events.DiagnosticConfigProperties;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the polling data source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Polling is not the default behavior; by default, the SDK uses a streaming connection to receive feature flag
    /// data from LaunchDarkly. In polling mode, the SDK instead makes a new HTTP request to LaunchDarkly at regular
    /// intervals. HTTP caching allows it to avoid redundantly downloading data if there have been no changes, but
    /// polling is still less efficient than streaming and should only be used on the advice of LaunchDarkly support.
    /// </para>
    /// <para>
    /// To use polling mode, create a builder with <see cref="Components.PollingDataSource"/>, change its properties
    /// with the methods of this class, and pass it to <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
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
    public sealed class PollingDataSourceBuilder : IDataSourceFactory, IDiagnosticDescription
    {
        /// <summary>
        /// The default value for <see cref="PollInterval(TimeSpan)"/>: 30 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(30);

        internal Uri _baseUri = null;
        internal TimeSpan _pollInterval = DefaultPollInterval;

        /// <summary>
        /// Deprecated method for setting a custom base URI for the polling service.
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
        /// <param name="baseUri">the base URI of the polling service; null to use the default</param>
        /// <returns>the builder</returns>
        /// <seealso cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)"/>
        [Obsolete("Use ConfigurationBuilder.ServiceEndpoints instead")]
        public PollingDataSourceBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
            return this;
        }

        /// <summary>
        /// Sets the interval at which the SDK will poll for feature flag updates.
        /// </summary>
        /// <remarks>
        /// The default and minimum value is <see cref="DefaultPollInterval"/>. Values less than this will
        /// be set to the default.
        /// </remarks>
        /// <param name="pollInterval">the polling interval</param>
        /// <returns>the builder</returns>
        public PollingDataSourceBuilder PollInterval(TimeSpan pollInterval)
        {
            _pollInterval = pollInterval.CompareTo(DefaultPollInterval) >= 0 ?
                pollInterval :
                DefaultPollInterval;
            return this;
        }

        // Exposed internally for testing
        internal PollingDataSourceBuilder PollIntervalNoMinimum(TimeSpan pollInterval)
        {
            _pollInterval = pollInterval;
            return this;
        }

        /// <inheritdoc/>
        public IDataSource CreateDataSource(LdClientContext context, IDataSourceUpdates dataSourceUpdates)
        {
            var configuredBaseUri = _baseUri ??
                StandardEndpoints.SelectBaseUri(context.Basic.ServiceEndpoints, e => e.PollingBaseUri, "Polling",
                    context.Basic.Logger);

            context.Basic.Logger.Warn("You should only disable the streaming API if instructed to do so by LaunchDarkly support");
            FeatureRequestor requestor = new FeatureRequestor(context, configuredBaseUri);
            return new PollingProcessor(
                context,
                requestor,
                dataSourceUpdates,
                _pollInterval
                );
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic) =>
            LdValue.BuildObject()
                .WithPollingProperties(
                    StandardEndpoints.IsCustomUri(basic.ServiceEndpoints, _baseUri, e => e.StreamingBaseUri),
                    _pollInterval
                )
                .Add("usingRelayDaemon", false) // this property is specific to the server-side SDK
                .Build();
    }
}

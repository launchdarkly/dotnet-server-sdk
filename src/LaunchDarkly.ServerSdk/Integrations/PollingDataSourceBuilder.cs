using System;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataSources;

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
    /// with the methods of this class, and pass it to <see cref="IConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
    /// </para>
    /// <para>
    /// Setting <see cref="IConfigurationBuilder.Offline(bool)"/> to <see langword="true"/> will supersede this
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
    public class PollingDataSourceBuilder : IDataSourceFactory, IDiagnosticDescription
    {
        internal static readonly Uri DefaultBaseUri = new Uri("https://app.launchdarkly.com");

        /// <summary>
        /// The default value for <see cref="PollInterval(TimeSpan)"/>: 30 seconds.
        /// </summary>
        public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(30);

        internal Uri _baseUri = DefaultBaseUri;
        internal TimeSpan _pollInterval = DefaultPollInterval;

        /// <summary>
        /// Sets a custom base URI for the polling service.
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
        /// <param name="baseUri">the base URI of the polling service; null to use the default</param>
        /// <returns>the builder</returns>
        public PollingDataSourceBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri ?? DefaultBaseUri;
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

        /// <inheritdoc/>
        public IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
        {
            context.Basic.Logger.Warn("You should only disable the streaming API if instructed to do so by LaunchDarkly support");
            FeatureRequestor requestor = new FeatureRequestor(context, _baseUri ?? DefaultBaseUri);
            return new PollingProcessor(
                context,
                requestor,
                dataStoreUpdates,
                _pollInterval
                );
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic)
        {
            return LdValue.BuildObject()
                .Add("streamingDisabled", true)
                .Add("customBaseURI",
                    !(_baseUri ?? DefaultBaseUri).Equals(DefaultBaseUri))
                .Add("customStreamURI", false)
                .Add("pollingIntervalMillis", _pollInterval.TotalMilliseconds)
                .Add("usingRelayDaemon", false)
                .Build();
        }
    }
}

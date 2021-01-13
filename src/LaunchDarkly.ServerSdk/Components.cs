using System;
using System.Linq;
using Common.Logging;
using LaunchDarkly.Common;
using LaunchDarkly.Client.Integrations;
using LaunchDarkly.Client.Interfaces;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Provides factories for the standard implementations of LaunchDarkly component interfaces.
    /// </summary>
    /// <remarks>
    /// Some of the configuration options in <see cref="IConfigurationBuilder"/> affect the entire SDK, but others are
    /// specific to one area of functionality, such as how the SDK receives feature flag updates or processes
    /// analytics events. For the latter, the standard way to specify a configuration is to call one of the
    /// static methods in <see cref="Components"/> (such as <see cref="Components.StreamingDataSource"/>),
    /// apply any desired configuration change to the object that that method returns (such as
    /// <see cref="StreamingDataSourceBuilder.InitialReconnectDelay(TimeSpan)"/>), and then use the
    /// corresponding method in <see cref="IConfigurationBuilder"/> (such as
    /// <see cref="IConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/>) to use that
    /// configured component in the SDK.
    /// </remarks>
    public static class Components
    {
        private static IFeatureStoreFactory _inMemoryFeatureStoreFactory = new InMemoryFeatureStoreFactory();
        private static IEventProcessorFactory _eventProcessorFactory = new DefaultEventProcessorFactory();
        private static IEventProcessorFactory _nullEventProcessorFactory = new NullEventProcessorFactory();
        private static IUpdateProcessorFactory _defaultUpdateProcessorFactory = new DefaultUpdateProcessorFactory();
        private static IUpdateProcessorFactory _nullUpdateProcessorFactory = new NullUpdateProcessorFactory();

        /// <summary>
        /// Returns a factory for the default in-memory implementation of <see cref="IFeatureStore"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Since it is the default, you do not normally need to call this method, unless you need to create
        /// a data store instance for testing purposes.
        /// </para>
        /// </remarks>
        public static IFeatureStoreFactory InMemoryDataStore => _inMemoryFeatureStoreFactory;

        /// <summary>
        /// Returns a configuration object that disables direct connection with LaunchDarkly for feature
        /// flag updates.
        /// </summary>
        /// <remarks>
        /// Passing this to <see cref="ConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/> causes the SDK
        /// not to retrieve feature flag data from LaunchDarkly, regardless of any other configuration. This is
        /// normally done if you are using the <a href="https://docs.launchdarkly.com/home/advanced/relay-proxy">Relay Proxy</a>
        /// in "daemon mode", where an external process-- the Relay Proxy-- connects to LaunchDarkly and populates
        /// a persistent data store with the feature flag data. The data store could also be populated by
        /// another process that is running the LaunchDarkly SDK. If there is no external process updating
        /// the data store, then the SDK will not have any feature flag data and will return application
        /// default values only.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataSource(Components.ExternalUpdatesOnly)
        ///         .DataStore(Components.PersistentDataStore(Redis.DataStore())) // assuming the Relay Proxy is using Redis
        ///         .Build();
        /// </code>
        /// </example>
        public static IUpdateProcessorFactory ExternalUpdatesOnly => _nullUpdateProcessorFactory;

        /// <summary>
        /// Returns a configuration object that disables analytics events.
        /// </summary>
        /// <remarks>
        /// Passing this to <see cref="ConfigurationBuilder.Events(IEventProcessorFactory)"/> causes
        /// the SDK to discard all analytics events and not send them to LaunchDarkly, regardless of
        /// any other configuration.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .Events(Components.NoEvents)
        ///         .Build();
        /// </code>
        /// </example>
        public static IEventProcessorFactory NoEvents => _nullEventProcessorFactory;

        /// <summary>
        /// Returns a configurable factory for using polling mode to get feature flag data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is not the default behavior; by default, the SDK uses a streaming connection to receive feature flag
        /// data from LaunchDarkly. In polling mode, the SDK instead makes a new HTTP request to LaunchDarkly at regular
        /// intervals. HTTP caching allows it to avoid redundantly downloading data if there have been no changes, but
        /// polling is still less efficient than streaming and should only be used on the advice of LaunchDarkly support.
        /// </para>
        /// <para>
        /// To use polling mode, call this method to obtain a builder, change its properties with the
        /// <see cref="PollingDataSourceBuilder"/> methods, and pass it to
        /// <see cref="ConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/>.
        /// </para>
        /// <para>
        /// Setting <see cref="ConfigurationBuilder.Offline(bool)"/> to <see langword="true"/> will superseded this
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
        /// <returns>a builder for setting polling connection properties</returns>
        /// <see cref="StreamingDataSource"/>
        /// <see cref="ConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/>
        public static PollingDataSourceBuilder PollingDataSource() =>
            new PollingDataSourceBuilder();

        /// <summary>
        /// Returns a configuration builder for analytics event delivery.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default configuration has events enabled with default settings. If you want to
        /// customize this behavior, call this method to obtain a builder, change its properties
        /// with the <see cref="EventProcessorBuilder"/> methods, and pass it to
        /// <see cref="ConfigurationBuilder.Events(IEventProcessorFactory)"/>.
        /// </para>
        /// <para>
        /// To completely disable sending analytics events, use <see cref="NoEvents"/> instead.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .Events(Components.SendEvents()
        ///             .Capacity(5000)
        ///             .FlushInterval(TimeSpan.FromSeconds(2)))
        ///         .Build();
        /// </code>
        /// </example>
        /// <returns>a builder for setting event properties</returns>
        public static EventProcessorBuilder SendEvents() => new EventProcessorBuilder();

        /// <summary>
        /// Returns a configurable factory for using streaming mode to get feature flag data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default, the SDK uses a streaming connection to receive feature flag data from LaunchDarkly. To use
        /// the default behavior, you do not need to call this method. However, if you want to customize the behavior
        /// of the connection, call this method to obtain a builder, change its properties with the
        /// <see cref="StreamingDataSourceBuilder"/> methods, and pass it to
        /// <see cref="ConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/>.
        /// </para>
        /// <para>
        /// Setting <see cref="ConfigurationBuilder.Offline(bool)"/> to <see langword="true"/> will superseded this
        /// setting and completely disable network requests.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataSource(Components.StreamingDataSource()
        ///             .InitialReconnectDelay(TimeSpan.FromMilliseconds(500)))
        ///         .Build();
        /// </code>
        /// </example>
        /// <returns>a builder for setting streaming connection properties</returns>
        /// <see cref="PollingDataSource"/>
        /// <see cref="ConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/>
        public static StreamingDataSourceBuilder StreamingDataSource() =>
            new StreamingDataSourceBuilder();

        /// <summary>
        /// Obsolete property for using the default analytics events implementation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If you pass the value to <see cref="IConfigurationBuilder.Events(IEventProcessorFactory)"/>,
        /// or you do not call <see cref="IConfigurationBuilder.Events(IEventProcessorFactory)"/> at all,
        /// the behavior is as follows:
        /// </para>
        /// <list type="bullet">
        /// <item> If you have set <see cref="IConfigurationBuilder.Offline(bool)"/> to true, the SDK
        /// will not send events to LaunchDarkly.</item>
        /// <item> Otherwise, it will send events, using the properties set by the deprecated events
        /// configuration methods such as <see cref="IConfigurationBuilder.EventCapacity(int)"/>.
        /// </list>
        /// </remarks>
        [Obsolete("Use SendEvents")]
        public static IEventProcessorFactory DefaultEventProcessor => _eventProcessorFactory;

        /// <summary>
        /// Obsolete property for using the default data source implementation based on deprecated
        /// configuration properties.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If you pass the value to <see cref="IConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/>,
        /// or you do not call <see cref="IConfigurationBuilder.DataSource(IUpdateProcessorFactory)"/> at all,
        /// the behavior is as follows:
        /// </para>
        /// <list type="bullet">
        /// <item> If you have set <see cref="IConfigurationBuilder.Offline(bool)"/> or
        /// <see cref="IConfigurationBuilder.UseLdd(bool)"/> to true, the SDK will not connect
        /// to LaunchDarkly for feature flag data.</item>
        /// <item> If you have set <see cref="IConfigurationBuilder.IsStreamingEnabled(bool)"/> to false,
        /// it will use polling mode-- equivalent to using <see cref="Components.PollingDataSource"/>
        /// with the options set by <see cref="IConfigurationBuilder.BaseUri(Uri)"/> and
        /// <see cref="IConfigurationBuilder.PollingInterval(TimeSpan)"/>.</item>
        /// <item> Otherwise, it will use streaming mode-- equivalent to using <see cref="Components.StreamingDataSource"/>
        /// with the options set by <see cref="IConfigurationBuilder.StreamUri(Uri)"/> and
        /// <see cref="IConfigurationBuilder.ReconnectTime(TimeSpan)"/>.
        /// </item>
        /// </remarks>
        [Obsolete("Use StreamingDataSource, PollingDataSource, or ExternalUpdatesOnly")]
        public static IUpdateProcessorFactory DefaultUpdateProcessor => _defaultUpdateProcessorFactory;

        /// <summary>
        /// Obsolete name for <see cref="InMemoryDataStore"/>.
        /// </summary>
        [Obsolete("Use InMemoryDataStore")]
        public static IFeatureStoreFactory InMemoryFeatureStore => _inMemoryFeatureStoreFactory;

        /// <summary>
        /// Obsolete name for <see cref="NoEvents"/>.
        /// </summary>
        [Obsolete("Use NoEvents")]
        public static IEventProcessorFactory NullEventProcessor => NoEvents;

        /// <summary>
        /// Obsolete name for <see cref="ExternalUpdatesOnly"/>.
        /// </summary>
        [Obsolete("Use ExternalUpdatesOnly")]
        public static IUpdateProcessorFactory NullUpdateProcessor => _nullUpdateProcessorFactory;
    }

    internal class DefaultEventProcessorFactory : IEventProcessorFactoryWithDiagnostics, IDiagnosticDescription
    {
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config) =>
            CreateEventProcessor(config, null);

        public IEventProcessor CreateEventProcessor(Configuration config, IDiagnosticStore diagnosticStore) {
            if (config.Offline)
            {
                return new NullEventProcessor();
            }
            var factory = GetConfiguredFactory(config);
            return ((IEventProcessorFactoryWithDiagnostics)factory).CreateEventProcessor(config, diagnosticStore);
        }

        private static EventProcessorBuilder GetConfiguredFactory(Configuration config) =>
            Components.SendEvents()
                .AllAttributesPrivate(config.AllAttributesPrivate)
                .BaseUri(config.EventsUri)
                .Capacity(config.EventCapacity)
                .DiagnosticRecordingInterval(config.DiagnosticRecordingInterval)
                .FlushInterval(config.EventFlushInterval)
                .InlineUsersInEvents(config.InlineUsersInEvents)
                .PrivateAttributeNames(config.PrivateAttributeNames.ToArray())
#pragma warning disable CS0618 // using obsolete property
                .SamplingInterval(config.EventSamplingInterval)
#pragma warning restore CS0618
                .UserKeysCapacity(config.UserKeysCapacity)
                .UserKeysFlushInterval(config.UserKeysFlushInterval);

        public LdValue DescribeConfiguration(Configuration config) =>
            GetConfiguredFactory(config).DescribeConfiguration(config);
    }

    internal class NullEventProcessorFactory : IEventProcessorFactory
    {
        internal static NullEventProcessorFactory Instance = new NullEventProcessorFactory();

        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config) =>
            new NullEventProcessor();
    }

    internal class InMemoryFeatureStoreFactory : IFeatureStoreFactory
    {
        IFeatureStore IFeatureStoreFactory.CreateFeatureStore()
        {
#pragma warning disable 0618 // deprecated constructor
            return new InMemoryFeatureStore();
#pragma warning restore 0618
        }
    }

    internal class DefaultUpdateProcessorFactory : IUpdateProcessorFactoryWithDiagnostics, IDiagnosticDescription
    {
        // Note, logger uses LDClient class name for backward compatibility
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        IUpdateProcessor IUpdateProcessorFactory.CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            return CreateUpdateProcessor(config, featureStore, null);
        }

        public IUpdateProcessor CreateUpdateProcessor(Configuration config, IFeatureStore featureStore, IDiagnosticStore diagnosticStore)
        {
            if (config.Offline)
            {
                Log.Info("Starting Launchdarkly client in offline mode.");
                return new NullUpdateProcessor();
            }
            if (config.UseLdd)
            {
                Log.Info("Starting LaunchDarkly in LDD mode. Skipping direct feature retrieval.");
                return new NullUpdateProcessor();
            }
            else
            {
                var factory = GetConfiguredFactory(config);
                if (factory is IUpdateProcessorFactoryWithDiagnostics upfwd)
                {
                    return upfwd.CreateUpdateProcessor(config, featureStore, diagnosticStore);
                }
                return factory.CreateUpdateProcessor(config, featureStore);
            }
        }

        private IUpdateProcessorFactory GetConfiguredFactory(Configuration config)
        {
            if (config.UseLdd)
            {
                return Components.ExternalUpdatesOnly;
            }
#pragma warning disable CS0612 // deprecated API
            if (config.IsStreamingEnabled)
            {
                return Components.StreamingDataSource()
                    .BaseUri(config.StreamUri)
                    .InitialReconnectDelay(config.ReconnectTime);
            }
            return Components.PollingDataSource()
                .BaseUri(config.BaseUri)
                .PollInterval(config.PollingInterval);
#pragma warning restore CS0612
        }

        public LdValue DescribeConfiguration(Configuration config) =>
            (GetConfiguredFactory(config) as IDiagnosticDescription).DescribeConfiguration(config);
    }

    internal class NullUpdateProcessorFactory : IUpdateProcessorFactory, IDiagnosticDescription
    {
        internal static readonly NullUpdateProcessorFactory Instance = new NullUpdateProcessorFactory();

        IUpdateProcessor IUpdateProcessorFactory.CreateUpdateProcessor(Configuration config, IFeatureStore featureStore)
        {
            if (config.Offline)
            {
                LdClient.Log.Info("Starting Launchdarkly client in offline mode.");
            }
            else
            {
                LdClient.Log.Info("LaunchDarkly client will not connect to LaunchDarkly for feature flag data");
            }
            return new NullUpdateProcessor();
        }

        public LdValue DescribeConfiguration(Configuration config)
        {
            // The difference between "offline" and "using the Relay daemon" is irrelevant from the data source's
            // point of view, but we describe them differently in diagnostic events. This is easy because if we were
            // configured to be completely offline... we wouldn't be sending any diagnostic events. Therefore, if
            // Components.ExternalUpdatesOnly was specified as the data source and we are sending a diagnostic
            // event, we can assume usingRelayDaemon should be true.
            return LdValue.BuildObject()
                .Add("customBaseURI", false)
                .Add("customStreamURI", false)
                .Add("streamingDisabled", false)
                .Add("usingRelayDaemon", true)
                .Build();
        }
    }
}

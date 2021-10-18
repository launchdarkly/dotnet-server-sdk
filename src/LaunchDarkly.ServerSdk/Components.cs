using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Provides factories for the standard implementations of LaunchDarkly component interfaces.
    /// </summary>
    /// <remarks>
    /// Some of the configuration options in <see cref="ConfigurationBuilder"/> affect the entire SDK, but others are
    /// specific to one area of functionality, such as how the SDK receives feature flag updates or processes
    /// analytics events. For the latter, the standard way to specify a configuration is to call one of the
    /// static methods in <see cref="Components"/> (such as <see cref="Components.StreamingDataSource"/>),
    /// apply any desired configuration change to the object that that method returns (such as
    /// <see cref="StreamingDataSourceBuilder.InitialReconnectDelay(TimeSpan)"/>), and then use the
    /// corresponding method in <see cref="ConfigurationBuilder"/> (such as
    /// <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/>) to use that
    /// configured component in the SDK.
    /// </remarks>
    public static class Components
    {
        /// <summary>
        /// Returns a configuration builder for the SDK's Big Segments feature.
        /// </summary>
        /// <remarks>
        /// <para>
        /// "Big Segments" are a specific type of user segments. For more information, read the LaunchDarkly
        /// documentation about user segments: https://docs.launchdarkly.com/home/users/segments
        /// </para>
        /// <para>
        /// After configuring this object, use <see cref="ConfigurationBuilder.BigSegments(IBigSegmentsConfigurationFactory)"/>
        /// to store it in your SDK configuration. For example, using the Redis integration:
        /// </para>
        /// <code>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .BigSegments(Components.BigSegments(Redis.DataStore().Prefix("app1"))
        ///             .UserCacheSize(2000))
        ///         .Build();
        /// </code>
        /// <para>
        /// You must always specify the <paramref name="storeFactory"/> parameter, to tell the SDK what database
        /// you are using. Several database integrations exist for the LaunchDarkly SDK, each with its own
        /// behavior and options specific to that database; this is described via some implementation of
        /// <see cref="IBigSegmentStoreFactory"/>. The <see cref="BigSegmentsConfigurationBuilder"/> adds
        /// configuration options for aspects of SDK behavior that are independent of the database. In the
        /// example above, <code>Prefix</code> is an option specifically for the Redis integration, whereas
        /// <code>UserCacheSize</code> is an option that can be used for any data store type.
        /// </para>
        /// </remarks>
        /// <param name="storeFactory">the factory for the underlying data store</param>
        /// <returns>a <see cref="BigSegmentsConfigurationBuilder"/></returns>
        public static BigSegmentsConfigurationBuilder BigSegments(IBigSegmentStoreFactory storeFactory) =>
            new BigSegmentsConfigurationBuilder(storeFactory);

        /// <summary>
        /// Returns a configuration object that disables direct connection with LaunchDarkly for feature
        /// flag updates.
        /// </summary>
        /// <remarks>
        /// Passing this to <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/> causes the SDK
        /// not to retrieve feature flag data from LaunchDarkly, regardless of any other configuration. This is
        /// normally done if you are using the <a href="https://docs.launchdarkly.com/home/relay-proxy">Relay Proxy</a>
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
        public static IDataSourceFactory ExternalUpdatesOnly => ComponentsImpl.NullDataSourceFactory.Instance;

        /// <summary>
        /// Returns a configuration builder for the SDK's networking configuration.
        /// </summary>
        /// <remarks>
        /// Passing this to <see cref="ConfigurationBuilder.Http(IHttpConfigurationFactory)"/> applies this
        /// configuration to all HTTP/HTTPS requests made by the SDK.
        /// </remarks>
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
        /// <returns>a configurable factory object</returns>
        public static HttpConfigurationBuilder HttpConfiguration() => new HttpConfigurationBuilder();

        /// <summary>
        /// Returns a factory for the default in-memory implementation of <see cref="IDataStore"/>.
        /// </summary>
        /// <remarks>
        /// Since it is the default, you do not normally need to call this method, unless you need to create
        /// a data store instance for testing purposes.
        /// </remarks>
        public static IDataStoreFactory InMemoryDataStore => ComponentsImpl.InMemoryDataStoreFactory.Instance;

        /// <summary>
        /// Returns a configuration builder for the SDK's logging configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Passing this to <see cref="ConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />,
        /// after setting any desired properties on the builder, applies this configuration to the SDK.
        /// </para>
        /// <para>
        /// The default behavior, if you do not change any properties, is to send log output to
        /// <see cref="Console.Error"/>, with a minimum level of <c>Info</c> (that is, <c>Debug</c> logging
        /// is disabled).
        /// </para>
        /// <para>
        /// For more about how logging works in the SDK, see the <a href="https://docs.launchdarkly.com/sdk/features/logging#net">SDK
        /// SDK reference guide</a>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().Level(LogLevel.Warn)))
        ///         .Build();
        /// </code>
        /// </example>
        /// <returns>a configurable factory object</returns>
        /// <seealso cref="ConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />
        /// <seealso cref="Components.Logging(ILogAdapter) "/>
        /// <seealso cref="Components.NoLogging" />
        public static LoggingConfigurationBuilder Logging() =>
            new LoggingConfigurationBuilder();

        /// <summary>
        /// Returns a configuration builder for the SDK's logging configuration, specifying the logging implementation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a shortcut for calling <see cref="Logging()"/> and then
        /// <see cref="LoggingConfigurationBuilder.Adapter(ILogAdapter)"/>, to specify a logging implementation
        /// other than the default one. For instance, in a .NET Core application you can use
        /// <c>LaunchDarkly.Logging.Logs.CoreLogging</c> to use the standard .NET Core logging framework.
        /// </para>
        /// <para>
        /// If you do not also specify a minimum logging level with <see cref="LoggingConfigurationBuilder.Level(LaunchDarkly.Logging.LogLevel)"/>,
        /// or with some other filtering mechanism that is defined by an external logging framework, then the
        /// log output will show all logging levels including <c>Debug</c>.
        /// </para>
        /// <para>
        /// For more about log adapters, see <see cref="LoggingConfigurationBuilder.Adapter(ILogAdapter)"/>.
        /// </para>
        /// <para>
        /// For more about how logging works in the SDK, see the <a href="https://docs.launchdarkly.com/sdk/features/logging#net">SDK
        /// SDK reference guide</a>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .Logging(Components.Logging(Logs.CoreLogging(coreLoggingFactory)))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="adapter">an <c>ILogAdapter</c> for the desired logging implementation</param>
        /// <returns>a configurable factory object</returns>
        /// <seealso cref="ConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />
        /// <seealso cref="LoggingConfigurationBuilder.Adapter(ILogAdapter)" />
        /// <seealso cref="Components.Logging() "/>
        /// <seealso cref="Components.NoLogging" />
        public static LoggingConfigurationBuilder Logging(ILogAdapter adapter) =>
            new LoggingConfigurationBuilder().Adapter(adapter);

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
        public static IEventProcessorFactory NoEvents =>
            ComponentsImpl.NullEventProcessorFactory.Instance;

        /// <summary>
        /// A configuration object that disables logging.
        /// </summary>
        /// <remarks>
        /// This is the same as <c>Logging(LaunchDarkly.Logging.Logs.None)</c>.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder(sdkKey)
        ///         .Logging(Components.NoLogging)
        ///         .Build();
        /// </code>
        /// </example>
        public static LoggingConfigurationBuilder NoLogging =>
            new LoggingConfigurationBuilder().Adapter(Logs.None);

        /// <summary>
        /// Returns a configurable factory for a persistent data store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method takes an <see cref="IPersistentDataStoreFactory"/> that is provided by
        /// some persistent data store implementation (i.e. a database integration), and converts
        /// it to a <see cref="PersistentDataStoreBuilder"/> which can be used to add
        /// caching behavior. You can then pass the <see cref="PersistentDataStoreBuilder"/>
        /// object to <see cref="ConfigurationBuilder.DataStore(IDataStoreFactory)"/> to use this
        /// configuration in the SDK. Example usage:
        /// </para>
        /// <code>
        ///     var myStore = Components.PersistentDataStore(Redis.FeatureStore())
        ///         .CacheTtl(TimeSpan.FromSeconds(45));
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataStore(myStore)
        ///         .Build();
        /// </code>
        /// <para>
        /// The method is overloaded because some persistent data store implementations
        /// use <see cref="IPersistentDataStoreFactory"/> while others use
        /// <see cref="IPersistentDataStoreAsyncFactory"/>.
        /// </para>
        /// </remarks>
        /// <param name="storeFactory">the factory for the underlying data store</param>
        /// <returns>a <see cref="PersistentDataStoreBuilder"/></returns>
        public static PersistentDataStoreBuilder PersistentDataStore(IPersistentDataStoreFactory storeFactory)
        {
            return new PersistentDataStoreBuilder(storeFactory);
        }

        /// <summary>
        /// Returns a configurable factory for a persistent data store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method takes an <see cref="IPersistentDataStoreFactory"/> that is provided by
        /// some persistent data store implementation (i.e. a database integration), and converts
        /// it to a <see cref="PersistentDataStoreBuilder"/> which can be used to add
        /// caching behavior. You can then pass the <see cref="PersistentDataStoreBuilder"/>
        /// object to <see cref="ConfigurationBuilder.DataStore(IDataStoreFactory)"/> to use this
        /// configuration in the SDK. Example usage:
        /// </para>
        /// <code>
        ///     var myStore = Components.PersistentDataStore(Redis.FeatureStore())
        ///         .CacheTtl(TimeSpan.FromSeconds(45));
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataStore(myStore)
        ///         .Build();
        /// </code>
        /// <para>
        /// The method is overloaded because some persistent data store implementations
        /// use <see cref="IPersistentDataStoreFactory"/> while others use
        /// <see cref="IPersistentDataStoreAsyncFactory"/>.
        /// </para>
        /// </remarks>
        /// <param name="storeFactory">the factory for the underlying data store</param>
        /// <returns>a <see cref="PersistentDataStoreBuilder"/></returns>
        public static PersistentDataStoreBuilder PersistentDataStore(IPersistentDataStoreAsyncFactory storeFactory)
        {
            return new PersistentDataStoreBuilder(storeFactory);
        }

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
        /// <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
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
        /// <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/>
        public static PollingDataSourceBuilder PollingDataSource() =>
            new PollingDataSourceBuilder();

        /// <summary>
        /// Returns a builder for configuring custom service URIs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Passing this to <see cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)" />,
        /// after setting any desired properties on the builder, applies this configuration to the SDK.
        /// </para>
        /// <para>
        /// Most applications will never need to use this method. The main use case is when connecting
        /// to a <a href="https://docs.launchdarkly.com/home/advanced/relay-proxy">LaunchDarkly
        /// Relay Proxy</a> instance. For more information, see <see cref="ServiceEndpointsBuilder"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder(mobileKey)
        ///         .ServiceEndpoints(Components.ServiceEndpoints().RelayProxy("http://my-relay-hostname:80"))
        ///         .Build();
        /// </code>
        /// </example>
        /// <returns>a configuration builder</returns>
        /// <seealso cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)" />
        public static ServiceEndpointsBuilder ServiceEndpoints() => new ServiceEndpointsBuilder();

        /// <summary>
        /// Returns a configurable factory for using streaming mode to get feature flag data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// By default, the SDK uses a streaming connection to receive feature flag data from LaunchDarkly. To use
        /// the default behavior, you do not need to call this method. However, if you want to customize the behavior
        /// of the connection, call this method to obtain a builder, change its properties with the
        /// <see cref="StreamingDataSourceBuilder"/> methods, and pass it to
        /// <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/>.
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
        /// <see cref="ConfigurationBuilder.DataSource(IDataSourceFactory)"/>
        public static StreamingDataSourceBuilder StreamingDataSource() =>
            new StreamingDataSourceBuilder();

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
    }
}

using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// A mutable object that uses the Builder pattern to specify properties for a <see cref="Configuration"/> object.
    /// </summary>
    /// <remarks>
    /// Obtain an instance of this class by calling <see cref="Configuration.Builder(string)"/>.
    /// 
    /// All of the builder methods for setting a configuration property return a reference to the same builder, so they can be
    /// chained together.
    /// </remarks>
    /// <example>
    /// <code>
    ///     var config = Configuration.Builder("my-sdk-key")
    ///         .StartWaitTime(TimeSpan.FromSeconds(5))
    ///         .Events(Components.SendEvents().Capacity(50000))
    ///         .Build();
    /// </code>
    /// </example>
    public sealed class ConfigurationBuilder
    {
        #region Internal properties

        internal static readonly TimeSpan DefaultStartWaitTime = TimeSpan.FromSeconds(10);

        // Let's try to keep these properties and methods alphabetical so they're easy to find. Note that they
        // are internal rather than private so that they can be read by the Configuration constructor.
        internal IBigSegmentsConfigurationFactory _bigSegmentsConfigurationFactory = null;
        internal IDataSourceFactory _dataSourceFactory = null;
        internal IDataStoreFactory _dataStoreFactory = null;
        internal bool _diagnosticOptOut = false;
        internal IEventProcessorFactory _eventProcessorFactory = null;
        internal IHttpConfigurationFactory _httpConfigurationFactory = null;
        internal ILoggingConfigurationFactory _loggingConfigurationFactory = null;
        internal bool _offline = false;
        internal string _sdkKey;
        internal ServiceEndpointsBuilder _serviceEndpointsBuilder = null;
        internal TimeSpan _startWaitTime = DefaultStartWaitTime;

        #endregion

        #region Internal constructors

        internal ConfigurationBuilder(string sdkKey)
        {
            _sdkKey = sdkKey;
        }

        internal ConfigurationBuilder(Configuration copyFrom)
        {
            _bigSegmentsConfigurationFactory = copyFrom.BigSegmentsConfigurationFactory;
            _dataSourceFactory = copyFrom.DataSourceFactory;
            _dataStoreFactory = copyFrom.DataStoreFactory;
            _diagnosticOptOut = copyFrom.DiagnosticOptOut;
            _eventProcessorFactory = copyFrom.EventProcessorFactory;
            _httpConfigurationFactory = copyFrom.HttpConfigurationFactory;
            _loggingConfigurationFactory = copyFrom.LoggingConfigurationFactory;
            _offline = copyFrom.Offline;
            _sdkKey = copyFrom.SdkKey;
            _serviceEndpointsBuilder = new ServiceEndpointsBuilder(copyFrom.ServiceEndpoints);
            _startWaitTime = copyFrom.StartWaitTime;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Creates a <see cref="Configuration"/> based on the properties that have been set on the builder.
        /// Modifying the builder after this point does not affect the returned <see cref="Configuration"/>.
        /// </summary>
        /// <returns>the configured <c>Configuration</c> object</returns>
        public Configuration Build()
        {
            return new Configuration(this);
        }

        /// <summary>
        /// Sets the configuration of the SDK's Big Segments feature.
        /// </summary>
        /// <remarks>
        /// <para>
        /// "Big Segments" are a specific type of user segments. For more information, read the LaunchDarkly
        /// documentation about user segments: https://docs.launchdarkly.com/home/users/segments
        /// </para>
        /// <para>
        /// If you are using this feature, you will normally specify a database implementation that matches how
        /// the LaunchDarkly Relay Proxy is configured, since the Relay Proxy manages the Big Segment data.
        /// </para>
        /// <para>
        /// By default, there is no implementation and Big Segments cannot be evaluated. In this case, any flag
        /// evaluation that references a Big Segment will behave as if no users are included in any Big
        /// Segments, and the <see cref="EvaluationReason"/> associated with any such flag evaluation will have
        /// a <see cref="EvaluationReason.BigSegmentsStatus"/> of <see cref="BigSegmentsStatus.NotConfigured"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     // This example uses the Redis integration
        ///     var config = Configuration.Builder(sdkKey)
        ///         .BigSegments(Components.BigSegments(Redis.DataStore().Prefix("app1"))
        ///             .UserCacheSize(2000))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="bigSegmentsConfigurationFactory">a configuration factory object returned by
        /// <see cref="Components.BigSegments(IBigSegmentStoreFactory)"/></param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder BigSegments(IBigSegmentsConfigurationFactory bigSegmentsConfigurationFactory)
        {
            _bigSegmentsConfigurationFactory = bigSegmentsConfigurationFactory;
            return this;
        }

        /// <summary>
        /// Sets the implementation of the component that receives feature flag data from LaunchDarkly,
        /// using a factory object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Depending on the implementation, the factory may be a builder that allows you to set other
        /// configuration options as well.
        /// </para>
        /// <para>
        /// The default is <see cref="Components.StreamingDataSource"/>. You may instead use
        /// <see cref="Components.PollingDataSource"/>, <see cref="Components.ExternalUpdatesOnly"/>, or a
        /// test fixture such as <see cref="FileData.DataSource"/>. See those methods for
        /// details on how to configure them.
        /// </para>
        /// </remarks>
        /// <param name="dataSourceFactory">the factory object</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder DataSource(IDataSourceFactory dataSourceFactory)
        {
            _dataSourceFactory = dataSourceFactory;
            return this;
        }

        /// <summary>
        /// Sets the data store implementation to be used for holding feature flags
        /// and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default is <see cref="Components.InMemoryDataStore"/>, but you may choose to use a custom
        /// implementation such as a database integration. For the latter, you will normally
        /// use <see cref="Components.PersistentDataStore(IPersistentDataStoreFactory)"/> in
        /// conjunction with some specific type for that integration.
        /// </para>
        /// <para>
        /// This is specified as a factory because the SDK normally manages the lifecycle of the
        /// data store; it will create an instance from the factory when an <see cref="LdClient"/>
        /// is created, and dispose of that instance when disposing of the client.
        /// </para>
        /// </remarks>
        /// <param name="dataStoreFactory">the factory object</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder DataStore(IDataStoreFactory dataStoreFactory)
        {
            _dataStoreFactory = dataStoreFactory;
            return this;
        }

        /// <summary>
        ///   Set to true to opt out of sending diagnostic events.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Unless the diagnosticOptOut field is set to true, the client will send some
        ///     diagnostics data to the LaunchDarkly servers in order to assist in the development
        ///     of future SDK improvements. These diagnostics consist of an initial payload
        ///     containing some details of SDK in use, the SDK's configuration, and the platform the
        ///     SDK is being run on, as well as payloads sent periodically with information on
        ///     irregular occurrences such as dropped events.
        ///   </para>
        /// </remarks>
        /// <param name="diagnosticOptOut">true to disable diagnostic events</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder DiagnosticOptOut(bool diagnosticOptOut)
        {
            _diagnosticOptOut = diagnosticOptOut;
            return this;
        }

        /// <summary>
        /// Sets the implementation of the component that processes analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.SendEvents"/>, but you may choose to set it to a customized
        /// <see cref="EventProcessorBuilder"/>, a custom implementation (for instance, a test fixture), or
        /// disable events with <see cref="Components.NoEvents"/>.
        /// </remarks>
        /// <param name="eventProcessorFactory">a builder/factory object for event configuration</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder Events(IEventProcessorFactory eventProcessorFactory)
        {
            _eventProcessorFactory = eventProcessorFactory;
            return this;
        }

        /// <summary>
        /// Sets the SDK's networking configuration, using a factory object. This object is normally a
        /// configuration builder obtained from <see cref="Components.HttpConfiguration()"/>, which has
        /// methods for setting individual HTTP-related properties.
        /// </summary>
        /// <param name="httpConfigurationFactory">a builder/factory object for HTTP configuration</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder Http(IHttpConfigurationFactory httpConfigurationFactory)
        {
            _httpConfigurationFactory = httpConfigurationFactory;
            return this;
        }

        /// <summary>
        /// Sets the SDK's logging configuration, using a factory object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This object is normally a configuration builder obtained from <see cref="Components.Logging()"/>
        /// which has methods for setting individual logging-related properties. As a shortcut for disabling
        /// logging, you may use <see cref="Components.NoLogging"/> instead. If all you want to do is to set
        /// the basic logging destination, and you do not need to set other logging properties, you can use
        /// <see cref="Logging(ILogAdapter)"/> instead.
        /// </para>
        /// <para>
        /// For more about how logging works in the SDK, see the <a href="https://docs.launchdarkly.com/sdk/features/logging#net">SDK
        /// SDK reference guide</a>.
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().Level(LogLevel.Warn)))
        ///         .Build();
        /// </example>
        /// <param name="loggingConfigurationFactory">the factory object</param>
        /// <returns>the same builder</returns>
        /// <seealso cref="Components.Logging()" />
        /// <seealso cref="Components.Logging(ILogAdapter) "/>
        /// <seealso cref="Components.NoLogging" />
        /// <seealso cref="Logging(ILogAdapter)"/>
        public ConfigurationBuilder Logging(ILoggingConfigurationFactory loggingConfigurationFactory)
        {
            _loggingConfigurationFactory = loggingConfigurationFactory;
            return this;
        }

        /// <summary>
        /// Sets the SDK's logging destination.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a shortcut for <c>Logging(Components.Logging(logAdapter))</c>. You can use it when you
        /// only want to specify the basic logging destination, and do not need to set other log properties.
        /// </para>
        /// <para>
        /// For more about how logging works in the SDK, see the <a href="https://docs.launchdarkly.com/sdk/features/logging#net">SDK
        /// SDK reference guide</a>.
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Logs.ToWriter(Console.Out))
        ///         .Build();
        /// </example>
        /// <param name="logAdapter">an <c>ILogAdapter</c> for the desired logging implementation</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder Logging(ILogAdapter logAdapter) =>
            Logging(Components.Logging(logAdapter));

        /// <summary>
        /// Sets whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        /// <param name="offline">true if the client should remain offline</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder Offline(bool offline)
        {
            _offline = offline;
            return this;
        }

        /// <summary>
        /// Sets the SDK key for your LaunchDarkly environment.
        /// </summary>
        /// <param name="sdkKey">the SDK key</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder SdkKey(string sdkKey)
        {
            _sdkKey = sdkKey;
            return this;
        }

        /// <summary>
        /// Sets the SDK's service URIs, using a configuration builder obtained from
        /// <see cref="Components.ServiceEndpoints"/>.
        /// </summary>
        /// <remarks>
        /// This overwrites any previous options set with <see cref="ServiceEndpoints(ServiceEndpointsBuilder)"/>.
        /// If you want to set multiple options, set them on the same <see cref="ServiceEndpointsBuilder"/>.
        /// </remarks>
        /// <param name="serviceEndpointsBuilder">the subconfiguration builder object</param>
        /// <returns>the main configuration builder</returns>
        /// <seealso cref="Components.ServiceEndpoints"/>
        /// <seealso cref="ServiceEndpointsBuilder"/>
        public ConfigurationBuilder ServiceEndpoints(ServiceEndpointsBuilder serviceEndpointsBuilder)
        {
            _serviceEndpointsBuilder = serviceEndpointsBuilder;
            return this;
        }

        /// <summary>
        /// Sets how long the client constructor will block awaiting a successful connection to
        /// LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// Setting this to 0 will not block and will cause the constructor to return
        /// immediately. The default value is 5 seconds.
        /// </remarks>
        /// <param name="startWaitTime">the length of time to wait</param>
        /// <returns>the same builder</returns>
        public ConfigurationBuilder StartWaitTime(TimeSpan startWaitTime)
        {
            _startWaitTime = startWaitTime;
            return this;
        }

        #endregion
    }
}

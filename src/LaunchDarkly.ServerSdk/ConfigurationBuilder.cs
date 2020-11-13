using System;
using System.Net.Http;
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
    public interface IConfigurationBuilder
    {
        /// <summary>
        /// Creates a <see cref="Configuration"/> based on the properties that have been set on the builder.
        /// Modifying the builder after this point does not affect the returned <see cref="Configuration"/>.
        /// </summary>
        /// <returns>the configured <c>Configuration</c> object</returns>
        Configuration Build();

        /// <summary>
        /// Sets the connection timeout. The default value is 10 seconds.
        /// </summary>
        /// <param name="connectionTimeout">the connection timeout</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder ConnectionTimeout(TimeSpan connectionTimeout);

        /// <summary>
        /// Sets the implementation of the component that receives feature flag data from LaunchDarkly,
        /// using a factory object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Depending on the implementation, the factory may be a builder that allows you to set other
        /// allows you to set other configuration options as well.
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
        IConfigurationBuilder DataSource(IDataSourceFactory dataSourceFactory);

        /// <summary>
        /// Sets the data store implementation to be used for holding feature flags
        /// and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default is <see cref="Components.InMemoryDataStore"/>, but you may choose to use a custom
        /// implementation such as a database integration. For the latter, you will normally
        /// use <see cref="Components.PersistentStore(IPersistentDataStoreFactory)"/> in
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
        IConfigurationBuilder DataStore(IDataStoreFactory dataStoreFactory);
        
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
        IConfigurationBuilder DiagnosticOptOut(bool diagnosticOptOut);

        /// <summary>
        /// Sets the implementation of the component that processes analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.SendEvents"/>, but you may choose to set it to a customized
        /// <see cref="EventProcessorBuilder"/>, a custom implementation (for instance, a test fixture), or
        /// disable events with <see cref="Components.NoEvents"/>.
        /// </remarks>
        /// <param name="factory">a builder/factory object for event configuration</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder Events(IEventProcessorFactory factory);

        /// <summary>
        /// Sets the object to be used for sending HTTP requests. This is exposed for testing purposes.
        /// </summary>
        /// <param name="httpMessageHandler">the <c>HttpMessageHandler</c> to use</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder HttpMessageHandler(HttpMessageHandler httpMessageHandler);

        /// <summary>
        /// Sets the SDK's logging configuration, using a factory object.
        /// </summary>
        /// <remarks>
        /// This object is normally a configuration builder obtained from <see cref="Components.Logging()"/>
        /// which has methods for setting individual logging-related properties. As a shortcut for disabling
        /// logging, you may use <see cref="Components.NoLogging"/> instead.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().Level(LogLevel.Warn)))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="factory">the factory object</param>
        /// <returns>the same builder</returns>
        /// <seealso cref="Components.Logging()" />
        /// <seealso cref="Components.Logging(ILogAdapter) "/>
        /// <seealso cref="Components.NoLogging" />
        IConfigurationBuilder Logging(ILoggingConfigurationFactory factory);

        /// <summary>
        /// Sets whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        /// <param name="offline">true if the client should remain offline</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder Offline(bool offline);

        /// <summary>
        /// Sets the timeout when reading data from the streaming connection.
        /// </summary>
        /// <remarks>
        /// The default value is 5 minutes.
        /// </remarks>
        /// <param name="readTimeout">the read timeout</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder ReadTimeout(TimeSpan readTimeout);

        /// <summary>
        /// Sets the SDK key for your LaunchDarkly environment.
        /// </summary>
        /// <param name="sdkKey">the SDK key</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder SdkKey(string sdkKey);

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
        IConfigurationBuilder StartWaitTime(TimeSpan startWaitTime);

        /// <summary>
        /// For use by wrapper libraries to set an identifying name for the wrapper being used. This
        /// will be sent in request headers during requests to the LaunchDarkly servers to allow
        /// recording metrics on the usage of these wrapper libraries.
        /// </summary>
        /// <param name="wrapperName">The name of the wrapper to include in request headers</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder WrapperName(string wrapperName);

        /// <summary>
        /// For use by wrapper libraries to set version to be included alongside a WrapperName. If
        /// WrapperName is unset or null, this field will be ignored.
        /// </summary>
        /// <param name="wrapperVersion">The version of the wrapper to include in request headers</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder WrapperVersion(string wrapperVersion);
    }

    class ConfigurationBuilder : IConfigurationBuilder
    {
        // Let's try to keep these properties and methods alphabetical so they're easy to find
        internal TimeSpan _connectionTimeout = Configuration.DefaultConnectionTimeout;
        internal IDataSourceFactory _dataSourceFactory = null;
        internal IDataStoreFactory _dataStoreFactory = null;
        internal bool _diagnosticOptOut = false;
        internal IEventProcessorFactory _eventProcessorFactory = null;
        internal HttpMessageHandler _httpMessageHandler = Configuration.DefaultMessageHandler;
        internal bool _isStreamingEnabled = true;
        internal ILogAdapter _logAdapter = null;
        internal ILoggingConfigurationFactory _loggingConfigurationFactory = null;
        internal bool _offline = false;
        internal TimeSpan _readTimeout = Configuration.DefaultReadTimeout;
        internal string _sdkKey;
        internal TimeSpan _startWaitTime = Configuration.DefaultStartWaitTime;
        internal bool _useLdd = false;
        internal string _wrapperName = null;
        internal string _wrapperVersion = null;

        public ConfigurationBuilder(string sdkKey)
        {
            _sdkKey = sdkKey;
        }

        public ConfigurationBuilder(Configuration copyFrom)
        {
            _connectionTimeout = copyFrom.ConnectionTimeout;
            _dataSourceFactory = copyFrom.DataSourceFactory;
            _dataStoreFactory = copyFrom.DataStoreFactory;
            _diagnosticOptOut = copyFrom.DiagnosticOptOut;
            _eventProcessorFactory = copyFrom.EventProcessorFactory;
            _httpMessageHandler = copyFrom.HttpMessageHandler;
            _loggingConfigurationFactory = copyFrom.LoggingConfigurationFactory;
            _offline = copyFrom.Offline;
            _readTimeout = copyFrom.ReadTimeout;
            _sdkKey = copyFrom.SdkKey;
            _startWaitTime = copyFrom.StartWaitTime;
            _wrapperName = copyFrom.WrapperName;
            _wrapperVersion = copyFrom.WrapperVersion;
        }

        public Configuration Build()
        {
            return new Configuration(this);
        }

        public IConfigurationBuilder ConnectionTimeout(TimeSpan connectionTimeout)
        {
            _connectionTimeout = connectionTimeout;
            return this;
        }

        public IConfigurationBuilder DataSource(IDataSourceFactory dataSourceFactory)
        {
            _dataSourceFactory = dataSourceFactory;
            return this;
        }

        public IConfigurationBuilder DataStore(IDataStoreFactory dataStoreFactory)
        {
            _dataStoreFactory = dataStoreFactory;
            return this;
        }
        
        public IConfigurationBuilder DiagnosticOptOut(bool diagnosticOptOut)
        {
            _diagnosticOptOut = diagnosticOptOut;
            return this;
        }

        public IConfigurationBuilder Events(IEventProcessorFactory eventProcessorFactory)
        {
            _eventProcessorFactory = eventProcessorFactory;
            return this;
        }
        
        public IConfigurationBuilder HttpMessageHandler(HttpMessageHandler httpMessageHandler)
        {
            _httpMessageHandler = httpMessageHandler;
            return this;
        }

        public IConfigurationBuilder IsStreamingEnabled(bool isStreamingEnabled)
        {
            _isStreamingEnabled = isStreamingEnabled;
            return this;
        }

        public IConfigurationBuilder Logging(ILoggingConfigurationFactory factory)
        {
            _loggingConfigurationFactory = factory;
            return this;
        }

        public IConfigurationBuilder Offline(bool offline)
        {
            _offline = offline;
            return this;
        }

        public IConfigurationBuilder ReadTimeout(TimeSpan readTimeout)
        {
            _readTimeout = readTimeout;
            return this;
        }

        public IConfigurationBuilder SdkKey(string sdkKey)
        {
            _sdkKey = sdkKey;
            return this;
        }

        public IConfigurationBuilder StartWaitTime(TimeSpan startWaitTime)
        {
            _startWaitTime = startWaitTime;
            return this;
        }

        public IConfigurationBuilder UseLdd(bool useLdd)
        {
            _useLdd = useLdd;
            return this;
        }

        public IConfigurationBuilder WrapperName(string wrapperName)
        {
            _wrapperName = wrapperName;
            return this;
        }

        public IConfigurationBuilder WrapperVersion(string wrapperVersion)
        {
            _wrapperVersion = wrapperVersion;
            return this;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Interfaces;
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
    ///         .AllAttributesPrivate(true)
    ///         .EventCapacity(1000)
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
        /// Sets whether or not user attributes (other than the key) should be private (not sent to
        /// the LaunchDarkly server).
        /// </summary>
        /// <remarks>
        /// If this is true, all of the user attributes will be private, not just the attributes specified with the
        /// <c>AndPrivate...</c> methods on the <see cref="User"/> object. By default, this is false.
        /// </remarks>
        /// <param name="allAttributesPrivate">true if all attributes should be private</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder AllAttributesPrivate(bool allAttributesPrivate);

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
        ///   Sets the interval at which periodic diagnostic events will be sent.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The default is every 15 minutes and the minimum is every minute.
        ///   </para>
        /// </remarks>
        /// <param name="diagnosticRecordingInterval">the diagnostic recording interval</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder DiagnosticRecordingInterval(TimeSpan diagnosticRecordingInterval);

        /// <summary>
        /// Sets the capacity of the events buffer.
        /// </summary>
        /// <remarks>
        /// The client buffers up to this many events in memory before flushing. If the capacity is exceeded
        /// before the buffer is flushed, events will be discarded. Increasing the capacity means that events
        /// are less likely to be discarded, at the cost of consuming more memory.
        /// </remarks>
        /// <param name="eventCapacity">the capacity of the events buffer</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder EventCapacity(int eventCapacity);

        /// <summary>
        /// Sets the time between flushes of the event buffer.
        /// </summary>
        /// <remarks>
        /// Decreasing the flush interval means that the event buffer is less likely to reach capacity. The
        /// default value is 5 seconds.
        /// </remarks>
        /// <param name="eventflushInterval">the flush interval</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder EventFlushInterval(TimeSpan eventflushInterval);

        /// <summary>
        /// Sets the implementation of <see cref="IEventProcessor"/> to be used for processing analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.DefaultEventProcessor"/>, but you may choose to use a custom
        /// implementation (for instance, a test fixture).
        /// </remarks>
        /// <param name="eventProcessorFactory">the factory object</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder EventProcessorFactory(IEventProcessorFactory eventProcessorFactory);
        
        /// <summary>
        /// Sets the base URL of the LaunchDarkly analytics event server.
        /// </summary>
        /// <param name="eventsUri">the events URI</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder EventsUri(Uri eventsUri);

        /// <summary>
        /// Sets the object to be used for sending HTTP requests. This is exposed for testing purposes.
        /// </summary>
        /// <param name="httpMessageHandler">the <c>HttpMessageHandler</c> to use</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder HttpMessageHandler(HttpMessageHandler httpMessageHandler);

        /// <summary>
        /// Sets whether to include full user details in every analytics event.
        /// </summary>
        /// <remarks>
        /// The default is false: events will only include the user key, except for one "index" event that
        /// provides the full details for the user.
        /// </remarks>
        /// <param name="inlineUsersInEvents">true or false</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder InlineUsersInEvents(bool inlineUsersInEvents);

        /// <summary>
        /// Sets the SDK's logging configuration, using a factory object.
        /// </summary>
        /// <remarks>
        /// This object is normally a configuration builder obtained from <see cref="Components.Logging()"/>
        /// which has methods for setting individual logging-related properties. As a shortcut for disabling
        /// logging, you may use <see cref="Components.NoLogging"/> instead.
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().Level(LogLevel.Warn)))
        ///         .Build();
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
        /// Marks an attribute name as private.
        /// </summary>
        /// <remarks>
        /// Any users sent to LaunchDarkly with this configuration active will have attributes with this name
        /// removed, even if you did not use the <c>AndPrivate...</c> methods on the <see cref="User"/> object.
        /// You may call this method repeatedly to mark multiple attributes as private.
        /// </remarks>
        /// <param name="privateAtributeName">the attribute name</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder PrivateAttribute(string privateAtributeName);

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
        /// Sets the number of user keys that the event processor can remember at any one time.
        /// </summary>
        /// <remarks>
        /// The event processor keeps track of recently seen user keys so that duplicate user details will not
        /// be sent in analytics events.
        /// </remarks>
        /// <param name="userKeysCapacity">the user key cache capacity</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder UserKeysCapacity(int userKeysCapacity);

        /// <summary>
        /// Sets the interval at which the event processor will clear its cache of known user keys.
        /// </summary>
        /// <remarks>
        /// The default value is five minutes.
        /// </remarks>
        /// <param name="userKeysFlushInterval">the flush interval</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder UserKeysFlushInterval(TimeSpan userKeysFlushInterval);

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
        internal bool _allAttributesPrivate = false;
        internal TimeSpan _connectionTimeout = Configuration.DefaultConnectionTimeout;
        internal IDataSourceFactory _dataSourceFactory = null;
        internal IDataStoreFactory _dataStoreFactory = null;
        internal bool _diagnosticOptOut = false;
        internal TimeSpan _diagnosticRecordingInterval = Configuration.DefaultDiagnosticRecordingInterval;
        internal int _eventCapacity = Configuration.DefaultEventCapacity;
        internal TimeSpan _eventFlushInterval = Configuration.DefaultEventFlushInterval;
        internal IEventProcessorFactory _eventProcessorFactory = null;
        internal Uri _eventsUri = Configuration.DefaultEventsUri;
        internal HttpMessageHandler _httpMessageHandler = new HttpClientHandler();
        internal bool _inlineUsersInEvents = false;
        internal bool _isStreamingEnabled = true;
        internal ILogAdapter _logAdapter = null;
        internal ILoggingConfigurationFactory _loggingConfigurationFactory = null;
        internal bool _offline = false;
        internal ISet<string> _privateAttributeNames = null;
        internal TimeSpan _readTimeout = Configuration.DefaultReadTimeout;
        internal string _sdkKey;
        internal TimeSpan _startWaitTime = Configuration.DefaultStartWaitTime;
        internal bool _useLdd = false;
        internal int _userKeysCapacity = Configuration.DefaultUserKeysCapacity;
        internal TimeSpan _userKeysFlushInterval = Configuration.DefaultUserKeysFlushInterval;
        internal string _wrapperName = null;
        internal string _wrapperVersion = null;

        public ConfigurationBuilder(string sdkKey)
        {
            _sdkKey = sdkKey;
        }

        public ConfigurationBuilder(Configuration copyFrom)
        {
            _allAttributesPrivate = copyFrom.AllAttributesPrivate;
            _connectionTimeout = copyFrom.ConnectionTimeout;
            _dataSourceFactory = copyFrom.DataSourceFactory;
            _dataStoreFactory = copyFrom.DataStoreFactory;
            _diagnosticOptOut = copyFrom.DiagnosticOptOut;
            _diagnosticRecordingInterval = copyFrom.DiagnosticRecordingInterval;
            _eventCapacity = copyFrom.EventCapacity;
            _eventFlushInterval = copyFrom.EventFlushInterval;
            _eventProcessorFactory = copyFrom.EventProcessorFactory;
            _eventsUri = copyFrom.EventsUri;
            _httpMessageHandler = copyFrom.HttpMessageHandler;
            _inlineUsersInEvents = copyFrom.InlineUsersInEvents;
            _loggingConfigurationFactory = copyFrom.LoggingConfigurationFactory;
            _offline = copyFrom.Offline;
            _privateAttributeNames = copyFrom.PrivateAttributeNames is null ? null :
                new HashSet<string>(copyFrom.PrivateAttributeNames);
            _readTimeout = copyFrom.ReadTimeout;
            _sdkKey = copyFrom.SdkKey;
            _startWaitTime = copyFrom.StartWaitTime;
            _userKeysCapacity = copyFrom.UserKeysCapacity;
            _userKeysFlushInterval = copyFrom.UserKeysFlushInterval;
            _wrapperName = copyFrom.WrapperName;
            _wrapperVersion = copyFrom.WrapperVersion;
        }

        public Configuration Build()
        {
            return new Configuration(this);
        }

        public IConfigurationBuilder AllAttributesPrivate(bool allAttributesPrivate)
        {
            _allAttributesPrivate = allAttributesPrivate;
            return this;
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

        public IConfigurationBuilder DiagnosticRecordingInterval(TimeSpan diagnosticRecordingInterval)
        {
            if (diagnosticRecordingInterval.CompareTo(Configuration.MinimumDiagnosticRecordingInterval) < 0)
            {
                //Log.Warn("DiagnosticRecordingInterval cannot be less than the minimum of 1 minute.");
                _diagnosticRecordingInterval = Configuration.MinimumDiagnosticRecordingInterval;
            }
            else
            {
                _diagnosticRecordingInterval = diagnosticRecordingInterval;
            }
            return this;
        }

        public IConfigurationBuilder EventCapacity(int eventCapacity)
        {
            _eventCapacity = eventCapacity;
            return this;
        }

        public IConfigurationBuilder EventFlushInterval(TimeSpan eventFlushInterval)
        {
            _eventFlushInterval = eventFlushInterval;
            return this;
        }

        public IConfigurationBuilder EventProcessorFactory(IEventProcessorFactory eventProcessorFactory)
        {
            _eventProcessorFactory = eventProcessorFactory;
            return this;
        }
        
        public IConfigurationBuilder EventsUri(Uri eventsUri)
        {
            _eventsUri = eventsUri;
            return this;
        }

        public IConfigurationBuilder HttpMessageHandler(HttpMessageHandler httpMessageHandler)
        {
            _httpMessageHandler = httpMessageHandler;
            return this;
        }

        public IConfigurationBuilder InlineUsersInEvents(bool inlineUsersInEvents)
        {
            _inlineUsersInEvents = inlineUsersInEvents;
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

        public IConfigurationBuilder PrivateAttribute(string privateAttributeName)
        {
            if (_privateAttributeNames is null)
            {
                _privateAttributeNames = new HashSet<string>();
            }
            _privateAttributeNames.Add(privateAttributeName);
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

        public IConfigurationBuilder UserKeysCapacity(int userKeysCapacity)
        {
            _userKeysCapacity = userKeysCapacity;
            return this;
        }

        public IConfigurationBuilder UserKeysFlushInterval(TimeSpan userKeysFlushInterval)
        {
            _userKeysFlushInterval = userKeysFlushInterval;
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

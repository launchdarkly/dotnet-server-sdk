using System;
using System.Collections.Generic;
using System.Net.Http;
using Common.Logging;
using LaunchDarkly.Client.Integrations;

namespace LaunchDarkly.Client
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
        /// Obsolete method for setting whether or not all user attributes should be private.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.AllAttributesPrivate(bool)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.AllAttributesPrivate")]
        IConfigurationBuilder AllAttributesPrivate(bool allAttributesPrivate);

        /// <summary>
        /// Obsolete method for setting the base URI for the polling service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method has no effect if streaming is enabled, or if you have used
        /// <see cref="DataSource(IUpdateProcessorFactory)"/> to specify the data source options. The
        /// preferred way to set the property is as follows:
        /// </para>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .DataSource(
        ///             Components.PollingDataSource()
        ///                 .BaseUri(baseUri)
        ///             )
        ///         .Build();
        /// </code>
        /// </remarks>
        /// <param name="baseUri">the base URI</param>
        /// <returns>the same builder</returns>
        /// <seealso cref="Components.PollingDataSource"/>
        /// <seealso cref="PollingDataSourceBuilder.BaseUri(Uri)"/>
        IConfigurationBuilder BaseUri(Uri baseUri);

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
        /// test fixture such as <see cref="Files.FileComponents.FileDataSource"/>. See those methods for
        /// details on how to configure them.
        /// </para>
        /// <para>
        /// Note that the interface is currently named <see cref="IUpdateProcessorFactory"/>, but in a future version it
        /// will be renamed to <c>IDataStoreFactory</c>.
        /// </para>
        /// </remarks>
        /// <param name="dataSourceFactory">the factory object</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder DataSource(IUpdateProcessorFactory dataSourceFactory);

        /// <summary>
        /// Sets the data store implementation to be used for holding feature flags
        /// and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default is <see cref="Components.InMemoryDataStore"/>, but you may choose to use a custom
        /// implementation such as a database integration. For the latter, you will normally
        /// use a component from one of the integration packages such as <c>LaunchDarkly.ServerSdk.Redis</c>.
        /// </para>
        /// <para>
        /// This is specified as a factory because the SDK normally manages the lifecycle of the
        /// data store; it will create an instance from the factory when an <see cref="LdClient"/>
        /// is created, and dispose of that instance when disposing of the client.
        /// </para>
        /// <para>
        /// Note that the interface is currently named <see cref="IFeatureStoreFactory"/>, but in a future version it
        /// will be renamed to <c>IDataStoreFactory</c>.
        /// </para>
        /// </remarks>
        /// <param name="dataStoreFactory">the factory object</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder DataStore(IFeatureStoreFactory dataStoreFactory);

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
        /// Obsolete method for setting the interval at which periodic diagnostic events will be sent.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.DiagnosticRecordingInterval(TimeSpan)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.DiagnosticRecordingInterval")]
        IConfigurationBuilder DiagnosticRecordingInterval(TimeSpan diagnosticRecordingInterval);

        /// <summary>
        /// Obsolete method for setting the capacity of the events buffer.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.Capacity(int)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.Capacity")]
        IConfigurationBuilder EventCapacity(int eventCapacity);

        /// <summary>
        /// Obsolete method for setting the time between flushes of the event buffer.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.FlushInterval(TimeSpan)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.FlushInterval")]
        IConfigurationBuilder EventFlushInterval(TimeSpan eventflushInterval);

        /// <summary>
        /// Obsolete name for <see cref="Events(IEventProcessorFactory)"/>.
        /// </summary>
        /// <seealso cref="Events"/>
        [Obsolete("Use Events")]
        IConfigurationBuilder EventProcessorFactory(IEventProcessorFactory eventProcessorFactory);

        /// <summary>
        /// Sets the implementation of the component that processes analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.DefaultEventProcessor"/>, but you may choose to set it to a customized
        /// <see cref="EventProcessorBuilder"/>, a custom implementation (for instance, a test fixture), or
        /// disable events with <see cref="Components.NoEvents"/>.
        /// </remarks>
        /// <param name="eventProcessorFactory">a builder/factory object for event configuration</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder Events(IEventProcessorFactory eventProcessorFactory);

        /// <summary>
        /// Obsolete method to set the base URL of the LaunchDarkly analytics event server.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.BaseUri(Uri)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.BaseUri")]
        IConfigurationBuilder EventsUri(Uri eventsUri);

        /// <summary>
        /// Obsolete name for <see cref="DataStore(IFeatureStoreFactory)"/>.
        /// </summary>
        [Obsolete("Use DataStore")]
        IConfigurationBuilder FeatureStoreFactory(IFeatureStoreFactory featureStoreFactory);

        /// <summary>
        /// Sets the object to be used for sending HTTP requests. This is exposed for testing purposes.
        /// </summary>
        /// <param name="httpClientHandler">the <c>HttpClientHandler</c> to use</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder HttpClientHandler(HttpClientHandler httpClientHandler);

        /// <summary>
        /// Sets the connection timeout. The default value is 10 seconds.
        /// </summary>
        /// <param name="httpClientTimeout">the connection timeout</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder HttpClientTimeout(TimeSpan httpClientTimeout);

        /// <summary>
        /// Obsolete method for setting whether to include full user details in every analytics event.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.InlineUsersInEvents(bool)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.InlineUsersInEvents")]
        IConfigurationBuilder InlineUsersInEvents(bool inlineUsersInEvents);

        /// <summary>
        /// Obsolete method for enabling or disabling streaming mode.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The SDK uses streaming by default. Streaming should only be disabled on the advice of
        /// LaunchDarkly support.
        /// </para>
        /// <para>
        /// This method has no effect if you have used <see cref="DataSource(IUpdateProcessorFactory)"/>
        /// to specify the data source options. The preferred way to set the property is to use
        /// <see cref="DataSource(IUpdateProcessorFactory)"/> with either
        /// <see cref="Components.StreamingDataSource"/> or <see cref="Components.PollingDataSource"/>.
        /// </para>
        /// </remarks>
        /// <param name="isStreamingEnabled">true if the streaming API should be used</param>
        /// <returns>the same builder</returns>
        /// <seealso cref="Components.StreamingDataSource"/>
        /// <seealso cref="Components.PollingDataSource"/>
        [Obsolete("Use DataSource")]
        IConfigurationBuilder IsStreamingEnabled(bool isStreamingEnabled);

        /// <summary>
        /// Sets whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        /// <param name="offline">true if the client should remain offline</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder Offline(bool offline);

        /// <summary>
        /// Obsolete method for setting the polling interval in polling mode.
        /// </summary>
        /// <remarks>
        /// This method has no effect if you have used <see cref="DataSource(IUpdateProcessorFactory)"/>
        /// to specify the data source options. The preferred way to set the property is to use
        /// <see cref="DataSource(IUpdateProcessorFactory)"/> with
        /// <see cref="Components.PollingDataSource"/> and <see cref="PollingDataSourceBuilder.PollInterval(TimeSpan)"/>.
        /// </remarks>
        /// <param name="pollingInterval">the rule update polling interval</param>
        /// <returns>the same builder</returns>
        /// <seealso cref="Components.PollingDataSource"/>
        /// <seealso cref="PollingDataSourceBuilder.PollInterval(TimeSpan)"/>
        [Obsolete("Use Components.PollingDataSource and PollingDataSourceBuilder.PollInterval")]
        IConfigurationBuilder PollingInterval(TimeSpan pollingInterval);

        /// <summary>
        /// Obsolete method for marking an attribute name as private.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.PrivateAttributeNames(string[])"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.PrivateAttributeNames")]
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
        /// Obsolete method for setting the initial reconnect delay for the streaming connection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method has no effect if streaming is disabled, or if you have used
        /// <see cref="DataSource(IUpdateProcessorFactory)"/> to specify the data source options. The
        /// preferred way to set the property is as follows:
        /// </para>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .DataSource(
        ///             Components.StreamingDataSource()
        ///                 .InitialReconnectDelay(reconnectTime)
        ///             )
        ///         .Build();
        /// </code>
        /// </remarks>
        /// <param name="reconnectTime">the reconnect time base value</param>
        /// <returns>the same builder</returns>
        /// <seealso cref="Components.StreamingDataSource"/>
        /// <seealso cref="StreamingDataSourceBuilder.InitialReconnectDelay(TimeSpan)"/>
        [Obsolete("Use Components.StreamingDataSource and StreamingDataSourceBuilder.InitialReconnectDelay")]
        IConfigurationBuilder ReconnectTime(TimeSpan reconnectTime);

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
        /// Obsolete method for setting the base URI for the streaming service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method has no effect if you have used <see cref="DataSource(IUpdateProcessorFactory)"/> to
        /// specify the data source options. The preferred way to set the property is as follows:
        /// </para>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .DataSource(
        ///             Components.StreamingDataSource()
        ///                 .BaseUri(streamUri)
        ///             )
        ///         .Build();
        /// </code>
        /// </remarks>
        /// <param name="streamUri">the stream URI</param>
        /// <returns>the same builder</returns>
        /// <seealso cref="Components.StreamingDataSource"/>
        /// <seealso cref="StreamingDataSourceBuilder.BaseUri(Uri)"/>
        [Obsolete("Use Components.StreamingDataSource and StreamingDataSourceBuilder.BaseUri")]
        IConfigurationBuilder StreamUri(Uri streamUri);

        /// <summary>
        /// Obsolete name for <see cref="DataSource(IUpdateProcessorFactory)"/>.
        /// </summary>
        /// <param name="updateProcessorFactory">the factory object</param>
        /// <returns>the same builder</returns>
        [Obsolete("Use DataSource")]
        IConfigurationBuilder UpdateProcessorFactory(IUpdateProcessorFactory updateProcessorFactory);

        /// <summary>
        /// Obsolete method for setting whether this client should use the <a href="https://docs.launchdarkly.com/docs/the-relay-proxy">LaunchDarkly
        /// relay</a> in daemon mode, instead of subscribing to the streaming or polling API.
        /// </summary>
        /// <seealso cref="Components.ExternalUpdatesOnly"/>
        [Obsolete("Use Components.ExternalUpdatesOnly")]
        IConfigurationBuilder UseLdd(bool useLdd);

        /// <summary>
        /// Obsolete method for setting the number of user keys that the event processor can remember at any one time.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.UserKeysCapacity(int)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.UserKeysCapacity")]
        IConfigurationBuilder UserKeysCapacity(int userKeysCapacity);

        /// <summary>
        /// Obsolete method for setting the interval at which the event processor will clear its cache of known user keys.
        /// </summary>
        /// <seealso cref="Components.SendEvents"/>
        /// <seealso cref="EventProcessorBuilder.UserKeysFlushInterval(TimeSpan)"/>
        [Obsolete("Use Components.SendEvents and EventProcessorBuilder.UserKeysFlushInterval")]
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
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConfigurationBuilder));

        // Let's try to keep these properties and methods alphabetical so they're easy to find
        internal bool _allAttributesPrivate = false;
        internal Uri _baseUri = Configuration.DefaultUri;
        internal bool _diagnosticOptOut = false;
        internal TimeSpan _diagnosticRecordingInterval = Configuration.DefaultDiagnosticRecordingInterval;
        internal int _eventCapacity = Configuration.DefaultEventQueueCapacity;
        internal TimeSpan _eventFlushInterval = Configuration.DefaultEventQueueFrequency;
        internal IEventProcessorFactory _eventProcessorFactory = null;
        internal Uri _eventsUri = Configuration.DefaultEventsUri;
        internal IFeatureStoreFactory _featureStoreFactory = null;
        internal HttpClientHandler _httpClientHandler = new HttpClientHandler();
        internal TimeSpan _httpClientTimeout = Configuration.DefaultHttpClientTimeout;
        internal bool _inlineUsersInEvents = false;
        internal bool _isStreamingEnabled = true;
        internal bool _offline = false;
        internal TimeSpan _pollingInterval = Configuration.DefaultPollingInterval;
        internal ISet<string> _privateAttributeNames = null;
        internal TimeSpan _readTimeout = Configuration.DefaultReadTimeout;
        internal TimeSpan _reconnectTime = Configuration.DefaultReconnectTime;
        internal string _sdkKey;
        internal TimeSpan _startWaitTime = Configuration.DefaultStartWaitTime;
        internal Uri _streamUri = Configuration.DefaultStreamUri;
        internal IUpdateProcessorFactory _updateProcessorFactory = null;
        internal bool _useLdd = false;
        internal int _userKeysCapacity = Configuration.DefaultUserKeysCapacity;
        internal TimeSpan _userKeysFlushInterval = Configuration.DefaultUserKeysFlushInterval;
        internal string _wrapperName = null;
        internal string _wrapperVersion = null;
        internal int _eventSamplingInterval = 0;     // deprecated Configuration property, settable only by copying
        internal IFeatureStore _featureStore = null; // deprecated Configuration property, settable only by copying

        public ConfigurationBuilder(string sdkKey)
        {
            _sdkKey = sdkKey;
        }

        public ConfigurationBuilder(Configuration copyFrom)
        {
#pragma warning disable 0612 // using obsolete properties
#pragma warning disable 0618 // using obsolete properties
            _allAttributesPrivate = copyFrom.AllAttributesPrivate;
            _baseUri = copyFrom.BaseUri;
            _diagnosticOptOut = copyFrom.DiagnosticOptOut;
            _diagnosticRecordingInterval = copyFrom.DiagnosticRecordingInterval;
            _eventCapacity = copyFrom.EventCapacity;
            _eventFlushInterval = copyFrom.EventFlushInterval;
            _eventProcessorFactory = copyFrom.EventProcessorFactory;
            _eventSamplingInterval = copyFrom.EventSamplingInterval;
            _eventsUri = copyFrom.EventsUri;
            _featureStore = copyFrom.FeatureStore;
            _featureStoreFactory = copyFrom.FeatureStoreFactory;
            _httpClientHandler = copyFrom.HttpClientHandler;
            _httpClientTimeout = copyFrom.HttpClientTimeout;
            _inlineUsersInEvents = copyFrom.InlineUsersInEvents;
            _isStreamingEnabled = copyFrom.IsStreamingEnabled;
            _offline = copyFrom.Offline;
            _pollingInterval = copyFrom.PollingInterval;
            _privateAttributeNames = copyFrom.PrivateAttributeNames is null ? null :
                new HashSet<string>(copyFrom.PrivateAttributeNames);
            _readTimeout = copyFrom.ReadTimeout;
            _reconnectTime = copyFrom.ReconnectTime;
            _sdkKey = copyFrom.SdkKey;
            _startWaitTime = copyFrom.StartWaitTime;
            _streamUri = copyFrom.StreamUri;
            _updateProcessorFactory = copyFrom.UpdateProcessorFactory;
            _useLdd = copyFrom.UseLdd;
            _userKeysCapacity = copyFrom.UserKeysCapacity;
            _userKeysFlushInterval = copyFrom.UserKeysFlushInterval;
            _wrapperName = copyFrom.WrapperName;
            _wrapperVersion = copyFrom.WrapperVersion;
#pragma warning restore 0618
#pragma warning restore 0612
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

        public IConfigurationBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
            return this;
        }

        public IConfigurationBuilder DataSource(IUpdateProcessorFactory dataSourceFactory)
        {
            _updateProcessorFactory = dataSourceFactory;
            return this;
        }

        public IConfigurationBuilder DataStore(IFeatureStoreFactory dataStoreFactory)
        {
            _featureStoreFactory = dataStoreFactory;
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
                Log.Warn("DiagnosticRecordingInterval cannot be less than the minimum of 1 minute.");
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

        public IConfigurationBuilder EventProcessorFactory(IEventProcessorFactory eventProcessorFactory) =>
            Events(eventProcessorFactory);

        public IConfigurationBuilder Events(IEventProcessorFactory eventProcessorFactory)
        {
            _eventProcessorFactory = eventProcessorFactory;
            return this;
        }

        public IConfigurationBuilder EventsUri(Uri eventsUri)
        {
            _eventsUri = eventsUri;
            return this;
        }

        public IConfigurationBuilder FeatureStoreFactory(IFeatureStoreFactory featureStoreFactory) =>
            DataStore(featureStoreFactory);

        public IConfigurationBuilder HttpClientHandler(HttpClientHandler httpClientHandler)
        {
            _httpClientHandler = httpClientHandler;
            return this;
        }

        public IConfigurationBuilder HttpClientTimeout(TimeSpan httpClientTimeout)
        {
            _httpClientTimeout = httpClientTimeout;
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

        public IConfigurationBuilder Offline(bool offline)
        {
            _offline = offline;
            return this;
        }

        public IConfigurationBuilder PollingInterval(TimeSpan pollingInterval)
        {
            if (pollingInterval.CompareTo(Configuration.DefaultPollingInterval) < 0)
            {
                Log.Warn("PollingInterval cannot be less than the default of 30 seconds.");
                _pollingInterval = Configuration.DefaultPollingInterval;
            }
            else
            {
                _pollingInterval = pollingInterval;
            }
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

        public IConfigurationBuilder ReconnectTime(TimeSpan reconnectTime)
        {
            _reconnectTime = reconnectTime;
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

        public IConfigurationBuilder StreamUri(Uri streamUri)
        {
            _streamUri = streamUri;
            return this;
        }

        public IConfigurationBuilder UpdateProcessorFactory(IUpdateProcessorFactory updateProcessorFactory) =>
            DataSource(updateProcessorFactory);

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

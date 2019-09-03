using System;
using System.Collections.Generic;
using System.Net.Http;
using Common.Logging;

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
        /// Sets the base URI of the LaunchDarkly server.
        /// </summary>
        /// <param name="baseUri">the base URI</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder BaseUri(Uri baseUri);

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
        /// Sets the implementation of <see cref="IFeatureStore"/> to be used for holding feature flags
        /// and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.InMemoryFeatureStore"/>, but you may choose to use a custom
        /// implementation.
        /// </remarks>
        /// <param name="featureStoreFactory">the factory object</param>
        /// <returns>the same builder</returns>
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
        /// Sets whether or not the streaming API should be used to receive flag updates.
        /// </summary>
        /// <remarks>
        /// This is true by default. Streaming should only be disabled on the advice of LaunchDarkly support.
        /// </remarks>
        /// <param name="isStreamingEnabled">true if the streaming API should be used</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder IsStreamingEnabled(bool isStreamingEnabled);

        /// <summary>
        /// Sets whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        /// <param name="offline">true if the client should remain offline</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder Offline(bool offline);

        /// <summary>
        /// Sets the polling interval (when streaming is disabled).
        /// </summary>
        /// <remarks>
        /// Values less than the default of 30 seconds will be changed to the default.
        /// </remarks>
        /// <param name="pollingInterval">the rule update polling interval</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder PollingInterval(TimeSpan pollingInterval);

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
        /// Sets the reconnect base time for the streaming connection.
        /// </summary>
        /// <remarks>
        /// The streaming connection uses an exponential backoff algorithm (with jitter) for reconnects, but
        /// will start the backoff with a value near the value specified here. The default value is 1 second.
        /// </remarks>
        /// <param name="reconnectTime">the reconnect time base value</param>
        /// <returns>the same builder</returns>
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
        /// Sets the base URI of the LaunchDarkly streaming server.
        /// </summary>
        /// <param name="streamUri">the stream URI</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder StreamUri(Uri streamUri);

        /// <summary>
        /// Sets the implementation of <see cref="IUpdateProcessor"/> to be used for receiving feature flag data.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.DefaultUpdateProcessor"/>, but you may choose to use a custom
        /// implementation (for instance, a test fixture).
        /// </remarks>
        /// <param name="updateProcessorFactory">the factory object</param>
        IConfigurationBuilder UpdateProcessorFactory(IUpdateProcessorFactory updateProcessorFactory);

        /// <summary>
        /// Sets whether this client should use the <a href="https://docs.launchdarkly.com/docs/the-relay-proxy">LaunchDarkly
        /// relay</a> in daemon mode, instead of subscribing to the streaming or polling API.
        /// </summary>
        /// <remarks>
        /// For this to work, you must also be using a
        /// <a href="https://docs.launchdarkly.com/docs/using-a-persistent-feature-store">persistent feature store</a>.
        /// </remarks>
        /// <param name="useLdd">true to use the relay in daemon mode; false to use streaming or polling</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder UseLdd(bool useLdd);

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
        ///   Sets the interval at which periodic diagnostic events will be sent.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     The default is every 15 minutes and the minimum is every minute.
        ///   </para>
        /// </remarks>
        /// <param name=""></param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder DiagnosticRecordingInterval(TimeSpan diagnosticRecordingInterval);

        /// <summary>
        ///   Set to true to opt out of sending diagnostic events.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     Unless the diagnosticOptOut field is set to true, the client will send some
        ///     diagnostics data to the LaunchDarkly servers in order to assist in the development
        ///     of future SDK improvements. These diagnostics consist of an initial payload
        ///     containing some details of SDK in use, the SDK's configuration, and the platform the
        ///     SDK is being run on; as well as payloads sent periodically with information on
        ///     irregular occurrences such as dropped events
        ///   </para>
        /// </remarks>
        /// <param name="diagnosticOptOut">true to disable diagnostic events</param>
        /// <returns>the same builder</returns>
        IConfigurationBuilder DiagnosticOptOut(bool diagnosticOptOut);

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

        internal bool _allAttributesPrivate = false;
        internal Uri _baseUri = Configuration.DefaultUri;
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
        internal int _eventSamplingInterval = 0;     // deprecated Configuration property, settable only by copying
        internal IFeatureStore _featureStore = null; // deprecated Configuration property, settable only by copying
        internal TimeSpan _diagnosticRecordingInterval = Configuration.DefaultDiagnosticRecordingInterval;
        internal bool _diagnosticOptOut = false;
        internal string _wrapperName = null;
        internal string _wrapperVersion = null;

        public ConfigurationBuilder(string sdkKey)
        {
            _sdkKey = sdkKey;
        }

        public ConfigurationBuilder(Configuration copyFrom)
        {
            _allAttributesPrivate = copyFrom.AllAttributesPrivate;
            _baseUri = copyFrom.BaseUri;
            _eventCapacity = copyFrom.EventCapacity;
            _eventFlushInterval = copyFrom.EventFlushInterval;
            _eventProcessorFactory = copyFrom.EventProcessorFactory;
#pragma warning disable 618
            _eventSamplingInterval = copyFrom.EventSamplingInterval;
#pragma warning restore 618
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
            _diagnosticRecordingInterval = copyFrom.DiagnosticRecordingInterval;
            _diagnosticOptOut = copyFrom.DiagnosticOptOut;
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

        public IConfigurationBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
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

        public IConfigurationBuilder FeatureStoreFactory(IFeatureStoreFactory featureStoreFactory)
        {
            _featureStoreFactory = featureStoreFactory;
            return this;
        }

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

        public IConfigurationBuilder UpdateProcessorFactory(IUpdateProcessorFactory updateProcessorFactory)
        {
            _updateProcessorFactory = updateProcessorFactory;
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

        public IConfigurationBuilder DiagnosticRecordingInterval(TimeSpan diagnosticRecordingInterval)
        {
            _diagnosticRecordingInterval = diagnosticRecordingInterval;
            return this;
        }

        public IConfigurationBuilder DiagnosticOptOut(bool diagnosticOptOut)
        {
            _diagnosticOptOut = diagnosticOptOut;
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

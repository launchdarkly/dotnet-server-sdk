using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Configuration options for <see cref="LdClient"/>. This class should normally be constructed with
    /// <see cref="Configuration.Builder(string)"/>.
    /// </summary>
    /// <remarks>
    /// Instances of <see cref="Configuration"/> are immutable once created. They can be created with the factory method
    /// <see cref="Configuration.Default(string)"/>, or using a builder pattern with <see cref="Configuration.Builder(string)"/>
    /// or <see cref="Configuration.Builder(Configuration)"/>.
    /// </remarks>
    public class Configuration
    {
        private readonly bool _allAttributesPrivate;
        private readonly Uri _baseUri;
        private readonly TimeSpan _connectionTimeout;
        private readonly IDataSourceFactory _dataSourceFactory;
        private readonly IDataStoreFactory _dataStoreFactory;
        private readonly TimeSpan _eventFlushInterval;
        private readonly int _eventCapacity;
        private readonly IEventProcessorFactory _eventProcessorFactory;
        private readonly Uri _eventsUri;
        private readonly HttpMessageHandler _httpMessageHandler;
        private readonly bool _inlineUsersInEvents;
        private readonly bool _isStreamingEnabled;
        private readonly bool _offline;
        private readonly TimeSpan _pollingInterval;
        private readonly ImmutableHashSet<string> _privateAttributeNames;
        private readonly TimeSpan _readTimeout;
        private readonly TimeSpan _reconnectTime;
        private readonly string _sdkKey;
        private readonly TimeSpan _startWaitTime;
        private readonly Uri _streamUri;
        private readonly bool _useLdd;
        private readonly int _userKeysCapacity;
        private readonly TimeSpan _userKeysFlushInterval;

        /// <summary>
        /// The base URI of the LaunchDarkly server.
        /// </summary>
        public Uri BaseUri => _baseUri;
        /// <summary>
        /// The base URL of the LaunchDarkly streaming server.
        /// </summary>
        public Uri StreamUri => _streamUri;
        /// <summary>
        /// The base URL of the LaunchDarkly analytics event server.
        /// </summary>
        public Uri EventsUri => _eventsUri;
        /// <summary>
        /// The SDK key for your LaunchDarkly environment.
        /// </summary>
        public string SdkKey => _sdkKey;
        /// <summary>
        /// Whether or not the streaming API should be used to receive flag updates.
        /// </summary>
        /// <remarks>
        /// This is true by default. Streaming should only be disabled on the advice of LaunchDarkly support.
        /// </remarks>
        public bool IsStreamingEnabled => _isStreamingEnabled;
        /// <summary>
        /// The capacity of the events buffer.
        /// </summary>
        /// <remarks>
        /// The client buffers up to this many events in memory before flushing. If the capacity is exceeded
        /// before the buffer is flushed, events will be discarded. Increasing the capacity means that events
        /// are less likely to be discarded, at the cost of consuming more memory.
        /// </remarks>
        public int EventCapacity => _eventCapacity;
        /// <summary>
        /// The time between flushes of the event buffer.
        /// </summary>
        /// <remarks>
        /// Decreasing the flush interval means that the event buffer is less likely to reach capacity.
        /// The default value is 5 seconds.
        /// </remarks>
        public TimeSpan EventFlushInterval => _eventFlushInterval;
        /// <summary>
        /// Set the polling interval (when streaming is disabled). The default value is 30 seconds.
        /// </summary>
        public TimeSpan PollingInterval => _pollingInterval;
        /// <summary>
        /// How long the client constructor will block awaiting a successful connection to
        /// LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// Setting this to 0 will not block and will cause the constructor to return immediately. The
        /// default value is 10 seconds.
        /// </remarks>
        public TimeSpan StartWaitTime => _startWaitTime;
        /// <summary>
        /// The timeout when reading data from the EventSource API. The default value is 5 minutes.
        /// </summary>
        public TimeSpan ReadTimeout => _readTimeout;
        /// <summary>
        /// The reconnect base time for the streaming connection.
        /// </summary>
        /// <remarks>
        /// The streaming connection uses an exponential backoff algorithm (with jitter) for reconnects,
        /// but will start the backoff with a value near the value specified here. The default value is 1 second.
        /// </remarks>
        public TimeSpan ReconnectTime => _reconnectTime;
        /// <summary>
        /// The connection timeout. The default value is 10 seconds.
        /// </summary>
        public TimeSpan ConnectionTimeout => _connectionTimeout;
        /// <summary>
        /// The object to be used for sending HTTP requests. This is exposed for testing purposes.
        /// </summary>
        public HttpMessageHandler HttpMessageHandler => _httpMessageHandler;
        /// <summary>
        /// Whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        public bool Offline => _offline;
        /// <summary>
        /// Whether or not user attributes (other than the key) should be private (not sent to
        /// the LaunchDarkly server).
        /// </summary>
        /// <remarks>
        /// If this is true, all of the user attributes will be private, not just attributes that are
        /// marked as private  on the <see cref="User"/> object. By default, this is false.
        /// </remarks>
        public bool AllAttributesPrivate => _allAttributesPrivate;
        /// <summary>
        /// Marks a set of attribute names as private.
        /// </summary>
        /// <remarks>
        /// Any users sent to LaunchDarkly with this configuration active will have attributes with these
        /// names removed, even if you did specify them as private on the <see cref="User"/> object.
        /// </remarks>
        public IImmutableSet<string> PrivateAttributeNames => _privateAttributeNames;
        /// <summary>
        /// The number of user keys that the event processor can remember at any one time, so that
        /// duplicate user details will not be sent in analytics events.
        /// </summary>
        public int UserKeysCapacity => _userKeysCapacity;
        /// <summary>
        /// The interval at which the event processor will reset its set of known user keys. The
        /// default value is five minutes.
        /// </summary>
        public TimeSpan UserKeysFlushInterval => _userKeysFlushInterval;
        /// <summary>
        /// True if full user details should be included in every analytics event.
        /// </summary>
        /// <remarks>
        /// The default is false (events will only include the user key, except for one "index" event
        /// that provides the full details for the user).
        /// </remarks>
        public bool InlineUsersInEvents => _inlineUsersInEvents;
        /// <summary>
        /// True if this client should use the <a href="https://docs.launchdarkly.com/docs/the-relay-proxy">LaunchDarkly
        /// relay</a> in daemon mode, instead of subscribing to the streaming or polling API.
        /// </summary>
        public bool UseLdd => _useLdd;
        /// <summary>
        /// A factory object that creates an implementation of <see cref="IDataStore"/>, to be used
        /// for holding feature flags and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.InMemoryDataStore"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IDataStoreFactory DataStoreFactory => _dataStoreFactory;
        /// <summary>
        /// A factory object that creates an implementation of <see cref="IEventProcessor"/>, which will
        /// process all analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.DefaultEventProcessor"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IEventProcessorFactory EventProcessorFactory => _eventProcessorFactory;
        /// <summary>
        /// A factory object that creates an implementation of <see cref="IDataSource"/>, which will
        /// receive feature flag data.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.DefaultDataSource"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IDataSourceFactory DataSourceFactory => _dataSourceFactory;
        /// <summary>
        /// A string that will be sent to LaunchDarkly to identify the SDK type.
        /// </summary>
        public string UserAgentType { get { return "DotNetClient"; } }
        /// <summary>
        /// Default value for <see cref="PollingInterval"/>.
        /// </summary>
        public static TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(30);
        /// <summary>
        /// Default value for <see cref="BaseUri"/>.
        /// </summary>
        internal static readonly Uri DefaultUri = new Uri("https://app.launchdarkly.com");
        /// <summary>
        /// Default value for <see cref="StreamUri"/>.
        /// </summary>
        internal static readonly Uri DefaultStreamUri = new Uri("https://stream.launchdarkly.com");
        /// <summary>
        /// Default value for <see cref="EventsUri"/>.
        /// </summary>
        internal static readonly Uri DefaultEventsUri = new Uri("https://events.launchdarkly.com");
        /// <summary>
        /// Default value for <see cref="EventCapacity"/>.
        /// </summary>
        internal static readonly int DefaultEventCapacity = 10000;
        /// <summary>
        /// Default value for <see cref="EventFlushInterval"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultEventFlushInterval = TimeSpan.FromSeconds(5);
        /// <summary>
        /// Default value for <see cref="StartWaitTime"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultStartWaitTime = TimeSpan.FromSeconds(10);
        /// <summary>
        /// Default value for <see cref="ReadTimeout"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMinutes(5);
        /// <summary>
        /// Default value for <see cref="ReconnectTime"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultReconnectTime = TimeSpan.FromSeconds(1);
        /// <summary>
        /// Default value for <see cref="ConnectionTimeout"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(10);
        /// <summary>
        /// Default value for <see cref="UserKeysCapacity"/>.
        /// </summary>
        internal static readonly int DefaultUserKeysCapacity = 1000;
        /// <summary>
        /// Default value for <see cref="UserKeysFlushInterval"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultUserKeysFlushInterval = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// Creates a configuration with all parameters set to the default.
        /// </summary>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <returns>a <c>Configuration</c> instance</returns>
        public static Configuration Default(string sdkKey)
        {
            return new ConfigurationBuilder(sdkKey).Build();
        }

        /// <summary>
        /// Creates an <see cref="IConfigurationBuilder"/> for constructing a configuration object using a fluent syntax.
        /// </summary>
        /// <remarks>
        /// This is the only method for building a <see cref="Configuration"/> if you are setting properties
        /// besides the <c>SdkKey</c>. The <see cref="IConfigurationBuilder"/> has methods for setting any number of
        /// properties, after which you call <see cref="IConfigurationBuilder.Build"/> to get the resulting
        /// <c>Configuration</c> instance.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .EventFlushInterval(TimeSpan.FromSeconds(90))
        ///         .StartWaitTime(TimeSpan.FromSeconds(5))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <returns>a builder object</returns>
        public static IConfigurationBuilder Builder(string sdkKey)
        {
            return new ConfigurationBuilder(sdkKey);
        }

        /// <summary>
        /// Creates an <see cref="IConfigurationBuilder"/> based on an existing configuration.
        /// </summary>
        /// <remarks>
        /// Modifying properties of the builder will not affect the original configuration object.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var configWithLargerEventCapacity = Configuration.Builder(originalConfig)
        ///         .EventCapacity(50000)
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="fromConfiguration">the existing configuration</param>
        /// <returns>a builder object</returns>
        public static IConfigurationBuilder Builder(Configuration fromConfiguration)
        {
            return new ConfigurationBuilder(fromConfiguration);
        }

        internal Configuration(ConfigurationBuilder builder)
        {
            _allAttributesPrivate = builder._allAttributesPrivate;
            _baseUri = builder._baseUri;
            _connectionTimeout = builder._connectionTimeout;
            _dataSourceFactory = builder._dataSourceFactory;
            _dataStoreFactory = builder._dataStoreFactory;
            _eventCapacity = builder._eventCapacity;
            _eventFlushInterval = builder._eventFlushInterval;
            _eventProcessorFactory = builder._eventProcessorFactory;
            _eventsUri = builder._eventsUri;
            _httpMessageHandler = builder._httpMessageHandler;
            _inlineUsersInEvents = builder._inlineUsersInEvents;
            _isStreamingEnabled = builder._isStreamingEnabled;
            _offline = builder._offline;
            _pollingInterval = builder._pollingInterval;
            _privateAttributeNames = builder._privateAttributeNames is null ?
                ImmutableHashSet.Create<string>() :
                builder._privateAttributeNames.ToImmutableHashSet();
            _readTimeout = builder._readTimeout;
            _reconnectTime = builder._reconnectTime;
            _sdkKey = builder._sdkKey;
            _streamUri = builder._streamUri;
            _startWaitTime = builder._startWaitTime;
            _useLdd = builder._useLdd;
            _userKeysCapacity = builder._userKeysCapacity;
            _userKeysFlushInterval = builder._userKeysFlushInterval;
        }
        
        internal IEventProcessorConfiguration EventProcessorConfiguration => new EventProcessorAdapter { Config = this };
        internal IHttpRequestConfiguration HttpRequestConfiguration => new HttpRequestAdapter { Config = this };
        internal IStreamManagerConfiguration StreamManagerConfiguration => new StreamManagerAdapter { Config = this };

        private struct EventProcessorAdapter : IEventProcessorConfiguration
        {
            internal Configuration Config { get; set; }
            public bool AllAttributesPrivate => Config.AllAttributesPrivate;
            public int EventCapacity => Config.EventCapacity;
            public TimeSpan EventFlushInterval => Config.EventFlushInterval;
            public Uri EventsUri => Config.EventsUri;
            public TimeSpan HttpClientTimeout => Config.ConnectionTimeout;
            public bool InlineUsersInEvents => Config.InlineUsersInEvents;
            public IImmutableSet<string> PrivateAttributeNames => Config.PrivateAttributeNames;
            public TimeSpan ReadTimeout => Config.ReadTimeout;
            public TimeSpan ReconnectTime => Config.ReconnectTime;
            public int UserKeysCapacity => Config.UserKeysCapacity;
            public TimeSpan UserKeysFlushInterval => Config.UserKeysFlushInterval;
        }

        private struct HttpRequestAdapter : IHttpRequestConfiguration
        {
            internal Configuration Config { get; set; }
            public string HttpAuthorizationKey => Config.SdkKey;
            public HttpMessageHandler HttpMessageHandler => Config.HttpMessageHandler;
        }

        private struct StreamManagerAdapter : IStreamManagerConfiguration
        {
            internal Configuration Config { get; set; }
            public string HttpAuthorizationKey => Config.SdkKey;
            public HttpMessageHandler HttpMessageHandler => Config.HttpMessageHandler;
            public TimeSpan HttpClientTimeout => Config.ConnectionTimeout;
            public TimeSpan ReadTimeout => Config.ReadTimeout;
            public TimeSpan ReconnectTime => Config.ReconnectTime;
            public Exception TranslateHttpException(Exception e) => e;
        }
    }
}

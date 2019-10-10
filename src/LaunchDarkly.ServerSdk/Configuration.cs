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
        /// <summary>
        /// The base URI of the LaunchDarkly server.
        /// </summary>
        public Uri BaseUri { get; internal set; }
        /// <summary>
        /// The base URL of the LaunchDarkly streaming server.
        /// </summary>
        public Uri StreamUri { get; internal set; }
        /// <summary>
        /// The base URL of the LaunchDarkly analytics event server.
        /// </summary>
        public Uri EventsUri { get; internal set; }
        /// <summary>
        /// The SDK key for your LaunchDarkly environment.
        /// </summary>
        public string SdkKey { get; internal set; }
        /// <summary>
        /// Whether or not the streaming API should be used to receive flag updates.
        /// </summary>
        /// <remarks>
        /// This is true by default. Streaming should only be disabled on the advice of LaunchDarkly support.
        /// </remarks>
        public bool IsStreamingEnabled { get; internal set; }
        /// <summary>
        /// The capacity of the events buffer.
        /// </summary>
        /// <remarks>
        /// The client buffers up to this many events in memory before flushing. If the capacity is exceeded
        /// before the buffer is flushed, events will be discarded. Increasing the capacity means that events
        /// are less likely to be discarded, at the cost of consuming more memory.
        /// </remarks>
        public int EventCapacity { get; internal set; }
        /// <summary>
        /// Deprecated name for <see cref="EventCapacity"/>.
        /// </summary>
        [Obsolete("Use EventCapacity")]
        public int EventQueueCapacity => EventCapacity;
        /// <summary>
        /// The time between flushes of the event buffer.
        /// </summary>
        /// <remarks>
        /// Decreasing the flush interval means that the event buffer is less likely to reach capacity.
        /// The default value is 5 seconds.
        /// </remarks>
        public TimeSpan EventFlushInterval { get; internal set; }
        /// <summary>
        /// Set the polling interval (when streaming is disabled). The default value is 30 seconds.
        /// </summary>
        public TimeSpan PollingInterval { get; internal set; }
        /// <summary>
        /// How long the client constructor will block awaiting a successful connection to
        /// LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// Setting this to 0 will not block and will cause the constructor to return immediately. The
        /// default value is 10 seconds.
        /// </remarks>
        public TimeSpan StartWaitTime { get; internal set; }
        /// <summary>
        /// The timeout when reading data from the EventSource API. The default value is 5 minutes.
        /// </summary>
        public TimeSpan ReadTimeout { get; internal set; }
        /// <summary>
        /// The reconnect base time for the streaming connection.
        /// </summary>
        /// <remarks>
        /// The streaming connection uses an exponential backoff algorithm (with jitter) for reconnects,
        /// but will start the backoff with a value near the value specified here. The default value is 1 second.
        /// </remarks>
        public TimeSpan ReconnectTime { get; internal set; }
        /// <summary>
        /// The connection timeout. The default value is 10 seconds.
        /// </summary>
        public TimeSpan HttpClientTimeout { get; internal set; }
        /// <summary>
        /// The object to be used for sending HTTP requests. This is exposed for testing purposes.
        /// </summary>
        public HttpMessageHandler HttpMessageHandler { get; internal set; }
        /// <summary>
        /// Whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        public bool Offline { get; internal set; }
        /// <summary>
        /// Whether or not user attributes (other than the key) should be private (not sent to
        /// the LaunchDarkly server).
        /// </summary>
        /// <remarks>
        /// If this is true, all of the user attributes will be private, not just attributes that are
        /// marked as private  on the <see cref="User"/> object. By default, this is false.
        /// </remarks>
        public bool AllAttributesPrivate { get; internal set; }
        /// <summary>
        /// Marks a set of attribute names as private.
        /// </summary>
        /// <remarks>
        /// Any users sent to LaunchDarkly with this configuration active will have attributes with these
        /// names removed, even if you did specify them as private on the <see cref="User"/> object.
        /// </remarks>
        public IImmutableSet<string> PrivateAttributeNames { get; internal set; }
        /// <summary>
        /// The number of user keys that the event processor can remember at any one time, so that
        /// duplicate user details will not be sent in analytics events.
        /// </summary>
        public int UserKeysCapacity { get; internal set; }
        /// <summary>
        /// The interval at which the event processor will reset its set of known user keys. The
        /// default value is five minutes.
        /// </summary>
        public TimeSpan UserKeysFlushInterval { get; internal set; }
        /// <summary>
        /// True if full user details should be included in every analytics event.
        /// </summary>
        /// <remarks>
        /// The default is false (events will only include the user key, except for one "index" event
        /// that provides the full details for the user).
        /// </remarks>
        public bool InlineUsersInEvents { get; internal set; }
        /// <summary>
        /// True if this client should use the <a href="https://docs.launchdarkly.com/docs/the-relay-proxy">LaunchDarkly
        /// relay</a> in daemon mode, instead of subscribing to the streaming or polling API.
        /// </summary>
        public bool UseLdd { get; internal set; }
        // (Used internally, was never public, will remove when WithFeatureStore is removed)
        internal IFeatureStore FeatureStore { get; set; }
        /// <summary>
        /// A factory object that creates an implementation of <see cref="IFeatureStore"/>, to be used
        /// for holding feature flags and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.InMemoryFeatureStore"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IFeatureStoreFactory FeatureStoreFactory { get; internal set; }
        /// <summary>
        /// A factory object that creates an implementation of <see cref="IEventProcessor"/>, which will
        /// process all analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.DefaultEventProcessor"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IEventProcessorFactory EventProcessorFactory { get; internal set; }
        /// <summary>
        /// A factory object that creates an implementation of <see cref="IUpdateProcessor"/>, which will
        /// receive feature flag data.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.DefaultUpdateProcessor"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IUpdateProcessorFactory UpdateProcessorFactory { get; internal set; }
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
        /// Default value for <see cref="HttpClientTimeout"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultHttpClientTimeout = TimeSpan.FromSeconds(10);
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
            AllAttributesPrivate = builder._allAttributesPrivate;
            BaseUri = builder._baseUri;
            EventCapacity = builder._eventCapacity;
            EventFlushInterval = builder._eventFlushInterval;
            EventProcessorFactory = builder._eventProcessorFactory;
            EventsUri = builder._eventsUri;
            FeatureStore = builder._featureStore;
            FeatureStoreFactory = builder._featureStoreFactory;
            HttpMessageHandler = builder._httpMessageHandler;
            HttpClientTimeout = builder._httpClientTimeout;
            InlineUsersInEvents = builder._inlineUsersInEvents;
            IsStreamingEnabled = builder._isStreamingEnabled;
            Offline = builder._offline;
            PollingInterval = builder._pollingInterval;
            PrivateAttributeNames = builder._privateAttributeNames is null ?
                ImmutableHashSet.Create<string>() :
                builder._privateAttributeNames.ToImmutableHashSet();
            ReadTimeout = builder._readTimeout;
            ReconnectTime = builder._reconnectTime;
            SdkKey = builder._sdkKey;
            StreamUri = builder._streamUri;
            StartWaitTime = builder._startWaitTime;
            UpdateProcessorFactory = builder._updateProcessorFactory;
            UseLdd = builder._useLdd;
            UserKeysCapacity = builder._userKeysCapacity;
            UserKeysFlushInterval = builder._userKeysFlushInterval;
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
            public TimeSpan HttpClientTimeout => Config.HttpClientTimeout;
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
            public TimeSpan HttpClientTimeout => Config.HttpClientTimeout;
            public TimeSpan ReadTimeout => Config.ReadTimeout;
            public TimeSpan ReconnectTime => Config.ReconnectTime;
            public Exception TranslateHttpException(Exception e) => e;
        }
    }
}

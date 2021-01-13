using System;
using System.Collections.Generic;
using System.Net.Http;
using LaunchDarkly.Client.Integrations;
using LaunchDarkly.Client.Interfaces;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Configuration options for <see cref="LdClient"/>. This class should normally be constructed with
    /// <see cref="Configuration.Builder(string)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note that the <see cref="Configuration"/> is currently mutable: even though the properties cannot be set
    /// directly, using the <see cref="ConfigurationExtensions"/> methods (such as
    /// <see cref="ConfigurationExtensions.WithStartWaitTime(Configuration, TimeSpan)"/>) modifies the
    /// original object.  In future versions of the SDK, this class will be changed to be immutable. The
    /// preferred method of setting configuration properties is to obtain a builder with
    /// <see cref="Configuration.Builder(string)"/>; the <see cref="ConfigurationExtensions"/>
    /// methods are now deprecated and will be removed once <c>Configuration</c> is immutable.
    /// </para>
    /// <para>
    /// If you modify properties of a <see cref="Configuration"/> after creating an <see cref="LdClient"/> with that
    /// <c>Configuration</c>, the behavior is undefined.
    /// </para>
    /// </remarks>
#pragma warning disable 618
    public class Configuration : IBaseConfiguration
#pragma warning restore 618
    {
        /// <summary>
        /// Obsolete property that is now set via <see cref="PollingDataSourceBuilder"/>.
        /// </summary>
        /// <seealso cref="Components.PollingDataSource"/>
        [Obsolete]
        public Uri BaseUri { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="StreamingDataSourceBuilder"/>.
        /// </summary>
        /// <seealso cref="Components.StreamingDataSource"/>
        [Obsolete]
        public Uri StreamUri { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        /// [Obsolete]
        public Uri EventsUri { get; internal set; }
        /// <summary>
        /// The SDK key for your LaunchDarkly environment.
        /// </summary>
        public string SdkKey { get; internal set; }
        /// <summary>
        /// Obsolete property for enabling or disabling streaming mode.
        /// </summary>
        /// <seealso cref="Components.StreamingDataSource"/>
        /// <seealso cref="Components.PollingDataSource"/>
        [Obsolete]
        public bool IsStreamingEnabled { get; internal set; }
        /// <summary>
        /// A factory object that creates an the component that will receive feature flag data.
        /// </summary>
        public IUpdateProcessorFactory DataSource { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public int EventCapacity { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public int EventQueueCapacity => EventCapacity;
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public TimeSpan EventFlushInterval { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public TimeSpan EventQueueFrequency => EventFlushInterval;
        /// <summary>
        /// Enables event sampling if non-zero.
        /// </summary>
        /// <remarks>
        /// When set to the default of zero, all analytics events are sent back to LaunchDarkly. When greater
        /// than zero, there is a 1 in <c>EventSamplingInterval</c> chance that events will be sent (example:
        /// if the interval is 20, on average 5% of events will be sent).
        /// </remarks>
        [Obsolete("This feature will be removed in a future version.")]
        public int EventSamplingInterval { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="PollingDataSourceBuilder"/>.
        /// </summary>
        /// <seealso cref="Components.PollingDataSource"/>
        [Obsolete]
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
        /// Obsolete property that is now set via <see cref="HttpConfigurationBuilder"/>.
        /// </summary>
        [Obsolete]
        public TimeSpan ReadTimeout { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="StreamingDataSourceBuilder"/>.
        /// </summary>
        /// <seealso cref="Components.StreamingDataSource"/>
        [Obsolete]
        public TimeSpan ReconnectTime { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="HttpConfigurationBuilder"/>.
        /// </summary>
        [Obsolete]
        public TimeSpan HttpClientTimeout { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="HttpConfigurationBuilder"/>.
        /// </summary>
        [Obsolete]
        public HttpClientHandler HttpClientHandler { get; internal set; }
        /// <summary>
        /// Whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        public bool Offline { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public bool AllAttributesPrivate { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public ISet<string> PrivateAttributeNames { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public int UserKeysCapacity { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public TimeSpan UserKeysFlushInterval { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public bool InlineUsersInEvents { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="Components.ExternalUpdatesOnly"/>.
        /// </summary>
        [Obsolete]
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
        /// Obsolete name for <see cref="DataSource"/>.
        /// </summary>
        [Obsolete("Use DataSource")]
        public IUpdateProcessorFactory UpdateProcessorFactory => DataSource;
        /// <summary>
        /// A string that will be sent to LaunchDarkly to identify the SDK type.
        /// </summary>
        public string UserAgentType { get { return "DotNetClient"; } }
        /// <summary>
        /// Obsolete property that is now set via <see cref="EventProcessorBuilder"/>.
        /// </summary>
        [Obsolete]
        public TimeSpan DiagnosticRecordingInterval { get; internal set; }
        /// <summary>
        /// True if diagnostic events have been disabled.
        /// </summary>
        public bool DiagnosticOptOut { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="HttpConfigurationBuilder"/>.
        /// </summary>
        [Obsolete]
        public string WrapperName { get; internal set; }
        /// <summary>
        /// Obsolete property that is now set via <see cref="HttpConfigurationBuilder"/>.
        /// </summary>
        [Obsolete]
        public string WrapperVersion { get; internal set; }

        internal IHttpConfigurationFactory HttpConfigurationFactory { get; private set; }

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
        /// Default value for <see cref="EventQueueCapacity"/>.
        /// </summary>
        internal static readonly int DefaultEventQueueCapacity = 10000;
        /// <summary>
        /// Default value for <see cref="EventQueueFrequency"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultEventQueueFrequency = TimeSpan.FromSeconds(5);
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
        /// Default value for <see cref="DiagnosticRecordingInterval"/>.
        /// </summary>
        internal static readonly TimeSpan DefaultDiagnosticRecordingInterval = TimeSpan.FromMinutes(15);
        /// <summary>
        /// Minimum value for <see cref="DiagnosticRecordingInterval"/>.
        /// </summary>
        internal static readonly TimeSpan MinimumDiagnosticRecordingInterval = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Creates a configuration with all parameters set to the default. Use extension methods
        /// to set additional parameters.
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
        /// This is the preferred method for building a <c>Configuration</c> if you are setting properties
        /// besides the <c>SdkKey</c>. The <c>ConfigurationBuilder</c> has methods for setting any number of
        /// properties, after which you call <see cref="IConfigurationBuilder.Build"/> to get the resulting
        /// <c>Configuration</c> instance.
        /// 
        /// This is different from using the extension methods such as
        /// <see cref="ConfigurationExtensions.WithStartWaitTime(Configuration, TimeSpan)"/>, which modify the properties
        /// of an existing <c>Configuration</c> instance. Those methods are now deprecated, because in a future
        /// version of the SDK, <c>Configuration</c> will be an immutable object.
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
#pragma warning disable 612
#pragma warning disable 618
            // Let's try to keep these alphabetical so it's easy to see if everything is here
            AllAttributesPrivate = builder._allAttributesPrivate;
            BaseUri = builder._baseUri;
            DataSource = builder._updateProcessorFactory;
            DiagnosticOptOut = builder._diagnosticOptOut;
            DiagnosticRecordingInterval = builder._diagnosticRecordingInterval;
            EventCapacity = builder._eventCapacity;
            EventFlushInterval = builder._eventFlushInterval;
            EventProcessorFactory = builder._eventProcessorFactory;
            EventSamplingInterval = builder._eventSamplingInterval;
            EventsUri = builder._eventsUri;
            FeatureStore = builder._featureStore;
            FeatureStoreFactory = builder._featureStoreFactory;
            HttpClientHandler = builder._httpClientHandler;
            HttpClientTimeout = builder._httpClientTimeout;
            HttpConfigurationFactory = builder._httpConfigurationFactory;
            InlineUsersInEvents = builder._inlineUsersInEvents;
            IsStreamingEnabled = builder._isStreamingEnabled;
            Offline = builder._offline;
            PollingInterval = builder._pollingInterval;
            PrivateAttributeNames = builder._privateAttributeNames is null ?
                new HashSet<string>() : new HashSet<string>(builder._privateAttributeNames);
            ReadTimeout = builder._readTimeout;
            ReconnectTime = builder._reconnectTime;
            SdkKey = builder._sdkKey;
            StreamUri = builder._streamUri;
            StartWaitTime = builder._startWaitTime;
            UseLdd = builder._useLdd;
            UserKeysCapacity = builder._userKeysCapacity;
            UserKeysFlushInterval = builder._userKeysFlushInterval;
            WrapperName = builder._wrapperName;
            WrapperVersion = builder._wrapperVersion;
#pragma warning restore 618
#pragma warning restore 612
        }

        internal IHttpConfiguration HttpConfiguration =>
            (HttpConfigurationFactory ?? new DefaultHttpConfigurationFactory()).CreateHttpConfiguration(this);

        internal IHttpRequestConfiguration HttpRequestConfiguration
        {
            get
            {
                var httpConfig = HttpConfiguration;
                return new HttpRequestConfigurationImpl
                {
                    HttpAuthorizationKey = SdkKey,
                    HttpClientHandler = httpConfig.MessageHandler as HttpClientHandler,
                    WrapperName = (httpConfig as IHttpConfigurationInternal)?.WrapperName,
                    WrapperVersion = (httpConfig as IHttpConfigurationInternal)?.WrapperVersion
                };
            }
        }

        private struct HttpRequestConfigurationImpl : IHttpRequestConfiguration
        {
            public string HttpAuthorizationKey { get; internal set; }
            public HttpClientHandler HttpClientHandler { get; internal set; }
            public string WrapperName { get; internal set; }
            public string WrapperVersion { get; internal set; }
        }
    }
}

using System;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Subsystems;

namespace LaunchDarkly.Sdk.Server
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
        #region Public properties

        /// <summary>
        /// A builder for <see cref="BigSegmentsConfiguration"/>.
        /// </summary>
        public IComponentConfigurer<BigSegmentsConfiguration> BigSegments { get; }

        /// <summary>
        /// A factory object that creates an implementation of <see cref="IDataSource"/>, which will
        /// receive feature flag data.
        /// </summary>
        public IComponentConfigurer<IDataSource> DataSource { get; }

        /// <summary>
        /// A factory object that creates an implementation of <see cref="IDataStore"/>, to be used
        /// for holding feature flags and related data received from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.InMemoryDataStore"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IComponentConfigurer<IDataStore> DataStore { get; }

        /// <summary>
        /// True if diagnostic events have been disabled.
        /// </summary>
        public bool DiagnosticOptOut { get; }

        /// <summary>
        /// A factory object that creates an implementation of <see cref="IEventProcessor"/>, which will
        /// process all analytics events.
        /// </summary>
        /// <remarks>
        /// The default is <see cref="Components.SendEvents"/>, but you may provide a custom
        /// implementation.
        /// </remarks>
        public IComponentConfigurer<IEventProcessor> Events { get; }

        /// <summary>
        /// A builder that creates an <see cref="HttpConfiguration"/>, defining the SDK's networking
        /// behavior.
        /// </summary>
        public IComponentConfigurer<HttpConfiguration> Http { get; }

        /// <summary>
        /// A builder that creates a <see cref="LoggingConfiguration"/>, defining the SDK's
        /// logging configuration.
        /// </summary>
        /// <remarks>
        /// SDK components should not use this property directly; instead, the SDK client will use it to create a
        /// logger instance which will be in <see cref="LdClientContext"/>.
        /// </remarks>
        public IComponentConfigurer<LoggingConfiguration> Logging { get; }

        /// <summary>
        /// Whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        public bool Offline { get; }

        /// <summary>
        /// The SDK key for your LaunchDarkly environment.
        /// </summary>
        public string SdkKey { get; }

        /// <summary>
        /// Defines the base service URIs used by SDK components.
        /// </summary>
        public ServiceEndpoints ServiceEndpoints { get; }

        /// <summary>
        /// How long the client constructor will block awaiting a successful connection to
        /// LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// Setting this to 0 will not block and will cause the constructor to return immediately. The
        /// default value is 10 seconds.
        /// </remarks>
        public TimeSpan StartWaitTime { get; }

        /// <summary>
        /// ApplicationInfo configuration which contains info about the application the SDK is running in.
        /// </summary>
        public ApplicationInfoBuilder ApplicationInfo { get; }

        /// <summary>
        /// WrapperInfo configuration which contains wrapper configuration. This is primarily intended for use by
        /// LaunchDarkly when developing wrapper SDKs.
        /// </summary>
        public WrapperInfoBuilder WrapperInfo { get; }


        /// <summary>
        /// Hooks configuration which contains a list of zero or more hooks to be executed by the SDK at points of interest.
        /// </summary>
        public HookConfigurationBuilder Hooks { get; }

        #endregion

        #region Public methods

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
        /// Creates a <see cref="ConfigurationBuilder"/> for constructing a configuration object using a fluent syntax.
        /// </summary>
        /// <remarks>
        /// This is the only method for building a <see cref="Configuration"/> if you are setting properties
        /// besides the <c>SdkKey</c>. The <see cref="ConfigurationBuilder"/> has methods for setting any number of
        /// properties, after which you call <see cref="ConfigurationBuilder.Build"/> to get the resulting
        /// <c>Configuration</c> instance.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .StartWaitTime(TimeSpan.FromSeconds(5))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="sdkKey">the SDK key for your LaunchDarkly environment</param>
        /// <returns>a builder object</returns>
        public static ConfigurationBuilder Builder(string sdkKey)
        {
            return new ConfigurationBuilder(sdkKey);
        }

        /// <summary>
        /// Creates an <see cref="ConfigurationBuilder"/> based on an existing configuration.
        /// </summary>
        /// <remarks>
        /// Modifying properties of the builder will not affect the original configuration object.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var configWithCustomEventProperties = Configuration.Builder(originalConfig)
        ///         .Events(Components.SendEvents().Capacity(50000))
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="fromConfiguration">the existing configuration</param>
        /// <returns>a builder object</returns>
        public static ConfigurationBuilder Builder(Configuration fromConfiguration)
        {
            return new ConfigurationBuilder(fromConfiguration);
        }

        #endregion

        #region Internal constructor

        internal Configuration(ConfigurationBuilder builder)
        {
            BigSegments = builder._bigSegments;
            DataSource = builder._dataSource;
            DataStore = builder._dataStore;
            DiagnosticOptOut = builder._diagnosticOptOut;
            Events = builder._events;
            Http = builder._http;
            Logging = builder._logging;
            Offline = builder._offline;
            SdkKey = builder._sdkKey;
            ServiceEndpoints = (builder._serviceEndpointsBuilder ?? Components.ServiceEndpoints()).Build();
            StartWaitTime = builder._startWaitTime;
            ApplicationInfo = builder._applicationInfo;
            WrapperInfo = builder._wrapperInfo;
            Hooks = builder._hooks;
        }

        #endregion
    }
}

using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Subsystems
{
    /// <summary>
    /// Encapsulates SDK client context when creating components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The SDK passes this object to component factories/configuration builders that implement the
    /// <see cref="IComponentConfigurer{T}"/> interface, to provide them with necessary configuration
    /// properties, as well as references to other components that they may need to access.
    /// </para>
    /// <para>
    /// This class also has non-public properties that are relevant only to internal SDK implementation
    /// code and are not accessible to custom components.
    /// </para>
    /// </remarks>
    public sealed class LdClientContext
    {
        /// <summary>
        /// A component that <see cref="IDataSource"/> implementations use to deliver data and status
        /// updates to the SDK.
        /// </summary>
        /// <remarks>
        /// This property is only set when the SDK is calling an <see cref="IDataSource"/> factory.
        /// Otherwise it is null.
        /// </remarks>
        public IDataSourceUpdates DataSourceUpdates { get; }

        /// <summary>
        /// A component that <see cref="IDataStore"/> implementations use to deliver status updates to
        /// the SDK.
        /// </summary>
        /// <remarks>
        /// This property is only set when the SDK is calling an <see cref="IDataStore"/> factory.
        /// Otherwise it is null.
        /// </remarks>
        public IDataStoreUpdates DataStoreUpdates { get; }

        /// <summary>
        /// The HTTP configuration for the current client instance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All SDK components that make HTTP requests must use this configuration unless there is a specific
        /// reason they cannot (for instance, streaming connections cannot use the ConnectTimeout property).
        /// </para>
        /// <para>
        /// This property is null during early stages of SDK initialization where the HTTP configuration has
        /// not yet been created.
        /// </para>
        /// </remarks>
        public HttpConfiguration Http { get; }

        /// <summary>
        /// The base logger for all SDK components to use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Components should use the methods of the <a href="https://github.com/launchdarkly/dotnet-logging">LaunchDarkly.Logging</a>
        /// API to write log messages which will be output or discarded as appropriate by the logging
        /// framework. This is the main logger for the SDK; components that want to tag their log messages
        /// with a more specific logger name can use the <c>SubLogger</c> method: for instance, if the
        /// base logger's name is "LaunchDarkly.Sdk", an event-processing component could use
        /// <c>SubLogger("Events")</c> to get a logger whose name is "LaunchDarkly.Sdk.Events".
        /// </para>
        /// <para>
        /// This property will never be null; if logging is disabled, it will be set to a stub logger
        /// that produces no output.
        /// </para>
        /// </remarks>
        public Logger Logger { get; }

        /// <summary>
        /// True if the SDK was configured to be completely offline.
        /// </summary>
        public bool Offline { get; }

        /// <summary>
        /// The configured SDK key.
        /// </summary>
        public string SdkKey { get; }

        /// <summary>
        /// Defines the base service URIs used by SDK components.
        /// </summary>
        public ServiceEndpoints ServiceEndpoints { get; }

        internal IDiagnosticStore DiagnosticStore { get; }

        internal TaskExecutor TaskExecutor { get; }

        /// <summary>
        /// Constructs a new instance with only the public properties.
        /// </summary>
        /// <remarks>
        /// This constructor is only for convenience in testing. It does not set internal properties that
        /// are normally computed in the LdClient constructor.
        /// </remarks>
        /// <param name="sdkKey">the SDK key</param>
        /// <param name="dataSourceUpdates">value for <see cref="DataSourceUpdates"/>, normally null</param>
        /// <param name="dataStoreUpdates">value for <see cref="DataStoreUpdates"/>, normally null</param>
        /// <param name="http">the HTTP configuration; if null, a default configuration is used</param>
        /// <param name="logger">the base logger; if null, logging is disabled</param>
        /// <param name="offline">true if the SDK should be entirely offline</param>
        /// <param name="serviceEndpoints">custom service endpoints; if null, defaults are used</param>
        public LdClientContext(
            string sdkKey,
            IDataSourceUpdates dataSourceUpdates,
            IDataStoreUpdates dataStoreUpdates,
            HttpConfiguration http,
            Logger logger,
            bool offline,
            ServiceEndpoints serviceEndpoints
            ) :
            this(
                sdkKey, dataSourceUpdates, dataStoreUpdates, http, logger, offline, serviceEndpoints,
                null,
                new TaskExecutor("test-sender", logger ?? Logs.None.Logger(""))
                )
        { }

        /// <summary>
        /// Basic constructor that sets only the SDK key and uses defaults for all other properties.
        /// </summary>
        /// <param name="sdkKey">the SDK key</param>
        public LdClientContext(string sdkKey) :
            this(sdkKey, null, null, null, null, false, null)
        { }

        internal LdClientContext(
            string sdkKey,
            IDataSourceUpdates dataSourceUpdates,
            IDataStoreUpdates dataStoreUpdates,
            HttpConfiguration http,
            Logger logger,
            bool offline,
            ServiceEndpoints serviceEndpoints,
            IDiagnosticStore diagnosticStore,
            TaskExecutor taskExecutor
            )
        {
            SdkKey = sdkKey;
            DataSourceUpdates = dataSourceUpdates;
            DataStoreUpdates = dataStoreUpdates;
            Http = http ?? DefaultHttpConfiguration();
            Logger = logger ?? Logs.None.Logger("");
            Offline = offline;
            ServiceEndpoints = serviceEndpoints ?? Components.ServiceEndpoints().Build();
            DiagnosticStore = diagnosticStore;
            TaskExecutor = taskExecutor;
        }

        internal LdClientContext WithDataSourceUpdates(IDataSourceUpdates newDataSourceUpdates) =>
            new LdClientContext(
                SdkKey,
                newDataSourceUpdates,
                DataStoreUpdates,
                Http,
                Logger,
                Offline,
                ServiceEndpoints,
                DiagnosticStore,
                TaskExecutor
                );

        internal LdClientContext WithDataStoreUpdates(IDataStoreUpdates newDataStoreUpdates) =>
            new LdClientContext(
                SdkKey,
                DataSourceUpdates,
                newDataStoreUpdates,
                Http,
                Logger,
                Offline,
                ServiceEndpoints,
                DiagnosticStore,
                TaskExecutor
                );

        internal LdClientContext WithDiagnosticStore(IDiagnosticStore newDiagnosticStore) =>
            new LdClientContext(
                SdkKey,
                DataSourceUpdates,
                DataStoreUpdates,
                Http,
                Logger,
                Offline,
                ServiceEndpoints,
                newDiagnosticStore,
                TaskExecutor
                );

        internal LdClientContext WithHttp(HttpConfiguration newHttp) =>
            new LdClientContext(
                SdkKey,
                DataSourceUpdates,
                DataStoreUpdates,
                newHttp,
                Logger,
                Offline,
                ServiceEndpoints,
                DiagnosticStore,
                TaskExecutor
                );

        internal LdClientContext WithLogger(Logger newLogger) =>
            new LdClientContext(
                SdkKey,
                DataSourceUpdates,
                DataStoreUpdates,
                Http,
                newLogger,
                Offline,
                ServiceEndpoints,
                DiagnosticStore,
                TaskExecutor
                );

        internal LdClientContext WithTaskExecutor(TaskExecutor newTaskExecutor) =>
            new LdClientContext(
                SdkKey,
                DataSourceUpdates,
                DataStoreUpdates,
                Http,
                Logger,
                Offline,
                ServiceEndpoints,
                DiagnosticStore,
                newTaskExecutor
                );

        private static HttpConfiguration DefaultHttpConfiguration() =>
            new HttpConfiguration(
                HttpConfigurationBuilder.DefaultConnectTimeout,
                null, null, null,
                HttpConfigurationBuilder.DefaultReadTimeout,
                HttpConfigurationBuilder.DefaultResponseStartTimeout
            );
    }
}

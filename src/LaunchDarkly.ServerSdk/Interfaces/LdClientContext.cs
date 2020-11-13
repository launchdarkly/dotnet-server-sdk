using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Logging;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Encapsulates SDK client context when creating components.
    /// </summary>
    /// <remarks>
    /// Factory interfaces like <see cref="IDataSourceFactory"/> receive this class as a parameter.
    /// Its public properties provide information about the SDK configuration and environment. The SDK
    /// may also include non-public properties that are relevant only when creating one of the built-in
    /// component types and are not accessible to custom components.
    /// </remarks>
    public class LdClientContext
    {
        /// <summary>
        /// The configuration for the current client instance.
        /// </summary>
        public Configuration Configuration { get; }

        /// <summary>
        /// The base logger for all SDK components to use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Components should use the methods of the <a href="https://github.com/launchdarkly/dotnet-logging">LaunchDarkly.Logging</a>
        /// API to write log messages which will be output or discarded as appropriate by the logging
        /// framework. This is the main logger for the SDK; components that want to tag their log messages
        /// with a more specific logger name can use the <c>SubLogger</c> method: for instance, if the
        /// base logger's name is "LaunchDarkly.Sdk.Server.LdClient", an event-processing component could
        /// use <c>SubLogger("Events")</c> to get a logger whose name is "LaunchDarkly.Sdk.Server.LdClient.Events".
        /// </para>
        /// <para>
        /// This property will never be null; if logging is disabled, it will be set to a stub logger
        /// that produces no output.
        /// </para>
        /// </remarks>
        public Logger Logger { get; }

        internal IDiagnosticStore DiagnosticStore { get; }

        /// <summary>
        /// Constructs a new instance with only the public properties, and no logging.
        /// </summary>
        /// <remarks>
        /// This constructor overload is only for convenience in testing.
        /// </remarks>
        /// <param name="configuration">the configuration for the current client instance</param>
        public LdClientContext(Configuration configuration) :
            this(configuration, Logs.None.Logger(""), null) { }

        /// <summary>
        /// Constructs a new instance with only the public properties.
        /// </summary>
        /// <param name="configuration">the configuration for the current client instance</param>
        /// <param name="logger">the base logger for all SDK components to use</param>
        public LdClientContext(Configuration configuration, Logger logger) :
            this(configuration, logger, null) { }

        internal LdClientContext(Configuration configuration, Logger logger,
            IDiagnosticStore diagnosticStore)
        {
            Configuration = configuration;
            Logger = logger;
            DiagnosticStore = diagnosticStore;
        }
    }
}

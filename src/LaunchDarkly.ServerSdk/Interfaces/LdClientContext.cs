using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;

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
    public sealed class LdClientContext
    {
        /// <summary>
        /// The SDK's basic global properties.
        /// </summary>
        public BasicConfiguration Basic { get; }

        /// <summary>
        /// The HTTP configuration for the current client instance.
        /// </summary>
        public HttpConfiguration Http { get; }

        internal IDiagnosticStore DiagnosticStore { get; }

        internal TaskExecutor TaskExecutor { get; }

        /// <summary>
        /// Constructs a new instance with only the public properties, and no logging.
        /// </summary>
        /// <remarks>
        /// This constructor overload is only for convenience in testing.
        /// </remarks>
        /// <param name="basic">the basic global SDK properties</param>
        /// <param name="configuration">the full configuration for the current client instance</param>
        public LdClientContext(
            BasicConfiguration basic,
            Configuration configuration
            ) :
            this(
                basic,
                (configuration.HttpConfigurationFactory ?? Components.HttpConfiguration()).CreateHttpConfiguration(basic),
                null,
                new TaskExecutor("test-sender", basic.Logger)
                ) { }

        internal LdClientContext(
            BasicConfiguration basic,
            HttpConfiguration http,
            IDiagnosticStore diagnosticStore,
            TaskExecutor taskExecutor
            )
        {
            Basic = basic;
            Http = http;
            DiagnosticStore = diagnosticStore;
            TaskExecutor = taskExecutor;
        }
    }
}

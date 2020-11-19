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
    public class LdClientContext
    {
        /// <summary>
        /// The SDK's basic global properties.
        /// </summary>
        public BasicConfiguration Basic { get; }

        /// <summary>
        /// The configuration for the current client instance.
        /// </summary>
        public Configuration Configuration { get; }

        internal IDiagnosticStore DiagnosticStore { get; }

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
            this(basic, configuration, null) { }

        internal LdClientContext(
            BasicConfiguration basic,
            Configuration configuration,
            IDiagnosticStore diagnosticStore
            )
        {
            Basic = basic;
            Configuration = configuration;
            DiagnosticStore = diagnosticStore;
        }
    }
}

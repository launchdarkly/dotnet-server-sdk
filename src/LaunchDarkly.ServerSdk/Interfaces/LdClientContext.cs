using LaunchDarkly.Sdk.Internal.Events;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Encapsulates SDK client context when creating components.
    /// </summary>
    /// <remarks>
    /// Factory interfaces like <see cref="IDataSourceFactory"/> receive this class as a parameter.
    /// Its only public member is the client configuration. The SDK may also include non-public
    /// properties that are relevant only when creating one of the built-in component types and are
    /// not accessible to custom components.
    /// </remarks>
    public class LdClientContext
    {
        private readonly Configuration _configuration;
        private readonly IDiagnosticStore _diagnosticStore;

        /// <summary>
        /// The configuration for the current client instance.
        /// </summary>
        public Configuration Configuration => _configuration;

        internal IDiagnosticStore DiagnosticStore => _diagnosticStore;

        /// <summary>
        /// Constructs a new instance with only the public properties.
        /// </summary>
        /// <param name="configuration">the configuration for the current client instance</param>
        public LdClientContext(Configuration configuration)
        {
            _configuration = configuration;
        }

        internal LdClientContext(Configuration configuration, IDiagnosticStore diagnosticStore)
        {
            _configuration = configuration;
            _diagnosticStore = diagnosticStore;
        }
    }
}

using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the SDK's logging behavior.
    /// </summary>
    /// <remarks>
    /// If you want to set non-default values for any of these properties, create a builder with
    /// <see cref="Components.Logging()"/>, change its properties with the methods of this class, and pass it
    /// to <see cref="IConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />.
    /// </remarks>
    /// <example>
    ///     var config = Configuration.Builder("my-sdk-key")
    ///         .Logging(Components.Logging().Level(LogLevel.Warn))
    ///         .Build();
    /// </example>
    public sealed class LoggingConfigurationBuilder : ILoggingConfigurationFactory
    {
        private ILogAdapter _logAdapter = null;
        private LogLevel? _minimumLevel = null;
        
        /// <summary>
        /// Creates a new builder with default properties.
        /// </summary>
        public LoggingConfigurationBuilder() { }

        /// <summary>
        /// Specifies the implementation of logging to use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <a href="https://github.com/launchdarkly/dotnet-logging">LaunchDarkly.Logging</a> API defines the
        /// <c>ILogAdapter</c> interface to specify where log output should be sent. By default, it is set to
        /// <c>Logs.ToConsole</c>, meaning that output will be sent to <c>Console.Error</c>. You may use other
        /// <c>LaunchDarkly.Logging.Logs</c> methods, or a custom implementation, to handle log output differently.
        /// For instance, in .NET Core, specify <c>Logs.CoreLogging</c> to use the standard .NET Core logging framework.
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().CoreLogging(coreLoggingFactory)))
        ///         .Build();
        /// </example>
        /// <param name="adapter">an <c>ILogAdapter</c> for the desired logging implementation</param>
        /// <returns>the same builder</returns>
        public LoggingConfigurationBuilder Adapter(ILogAdapter adapter)
        {
            _logAdapter = adapter;
            return this;
        }

        /// <summary>
        /// Specifies the lowest level of logging to enable.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This adds a log level filter that is applied regardless of what implementation of logging is
        /// being used, so that log messages at lower levels are suppressed. For instance, setting the
        /// minimum level to <see cref="LogLevel.Info"/> means that <c>Debug</c>-level output is disabled.
        /// External logging frameworks may also have their own mechanisms for setting a minimum log level.
        /// </para>
        /// <para>
        /// If you did not specify an <see cref="ILogAdapter"/> at all, so it is using the default <c>Console.Error</c>
        /// destination, then the default minimum logging level is <c>Info</c>.
        /// </para>
        /// <para>
        /// If you did specify an <see cref="ILogAdapter"/>, then the SDK does not apply a level filter by
        /// default. This is so as not to interfere with any other configuration that you may have set up
        /// in an external logging framework. However, you can still use this method to set a higher level
        /// so that any messages below that level will not be sent to the external framework at all.
        /// </para>
        /// </remarks>
        /// <param name="minimumLevel">the lowest level of logging to enable</param>
        /// <returns>the same builder</returns>
        public LoggingConfigurationBuilder Level(LogLevel minimumLevel)
        {
            _minimumLevel = minimumLevel;
            return this;
        }

        /// <inheritdoc/>
        public ILoggingConfiguration CreateLoggingConfiguration()
        {
            ILogAdapter adapter;
            if (_logAdapter is null)
            {
                adapter = Logs.ToConsole.Level(_minimumLevel ?? LogLevel.Info);
            }
            else
            {
                adapter = _minimumLevel.HasValue ?
                   _logAdapter.Level(_minimumLevel.Value) :
                    _logAdapter;
            }
            return new LoggingConfigurationImpl(adapter);
        }

        private sealed class LoggingConfigurationImpl : ILoggingConfiguration
        {
            public ILogAdapter LogAdapter { get; }

            internal LoggingConfigurationImpl(ILogAdapter logAdapter)
            {
                LogAdapter = logAdapter;
            }
        }
    }
}

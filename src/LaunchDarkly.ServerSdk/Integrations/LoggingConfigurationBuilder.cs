using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the SDK's logging behavior.
    /// </summary>
    /// <remarks>
    /// If you want to set non-default values for any of these properties, create a builder with
    /// <see cref="Components.Logging()"/>, change its properties with the methods of this class, and pass it
    /// to <see cref="ConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />.
    /// </remarks>
    /// <example>
    /// <code>
    ///     var config = Configuration.Builder("my-sdk-key")
    ///         .Logging(Components.Logging().Level(LogLevel.Warn))
    ///         .Build();
    /// </code>
    /// </example>
    public sealed class LoggingConfigurationBuilder : ILoggingConfigurationFactory
    {
        private ILogAdapter _logAdapter = null;
        private LogLevel? _minimumLevel = null;
        private TimeSpan? _logDataSourceOutageAsErrorAfter = null;

        /// <summary>
        /// The default value for <see cref="LogDataSourceOutageAsErrorAfter(TimeSpan?)"/>: one minute.
        /// </summary>
        public static readonly TimeSpan DefaultLogDataSourceAsErrorAfter = TimeSpan.FromMinutes(1);

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
        /// <code>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().CoreLogging(coreLoggingFactory)))
        ///         .Build();
        /// </code>
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
        /// minimum level to <see cref="LaunchDarkly.Logging.LogLevel.Info"/> means that <c>Debug</c>-level output is disabled.
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

        /// <summary>
        /// Sets the time threshold, if any, after which the SDK will log a data source outage at <c>Error</c>
        /// level instead of <c>Warn</c> level.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A data source outage means that an error condition, such as a network interruption or an error from
        /// the LaunchDarkly service, is preventing the SDK from receiving feature flag updates. Many outages are
        /// brief and the SDK can recover from them quickly; in that case it may be undesirable to log an
        /// <c>Error</c> line, which might trigger an unwanted automated alert depending on your monitoring
        /// tools. So, by default, the SDK logs such errors at <c>Warn</c> level. However, if the amount of time
        /// specified by this method elapses before the data source starts working again, the SDK will log an
        /// additional message at <c>Error</c> level to indicate that this is a sustained problem.
        /// </para>
        /// <para>
        /// The default is <see cref="DefaultLogDataSourceAsErrorAfter"/>. Setting it to <see langword="null"/>
        /// will disable this feature, so you will only get <c>Warn</c> messages.
        /// </para>
        /// </remarks>
        /// <param name="interval">the error logging threshold, or null</param>
        /// <returns>the same builder</returns>
        public LoggingConfigurationBuilder LogDataSourceOutageAsErrorAfter(TimeSpan? interval)
        {
            _logDataSourceOutageAsErrorAfter = interval;
            return this;
        }

        /// <inheritdoc/>
        public ILoggingConfiguration CreateLoggingConfiguration()
        {
            return new LoggingConfigurationImpl(this);
        }

        private sealed class LoggingConfigurationImpl : ILoggingConfiguration
        {
            public ILogAdapter LogAdapter { get; }
            public TimeSpan? LogDataSourceOutageAsErrorAfter { get; }

            internal LoggingConfigurationImpl(LoggingConfigurationBuilder builder)
            {
                if (builder._logAdapter is null)
                {
                    LogAdapter = Logs.ToConsole.Level(builder._minimumLevel ?? LogLevel.Info);
                }
                else
                {
                    LogAdapter = builder._minimumLevel.HasValue ?
                       builder._logAdapter.Level(builder._minimumLevel.Value) :
                        builder._logAdapter;
                }

                LogDataSourceOutageAsErrorAfter = builder._logDataSourceOutageAsErrorAfter;
            }
        }
    }
}

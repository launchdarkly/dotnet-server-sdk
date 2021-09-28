using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring the SDK's logging behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If you want to set non-default values for any of these properties, create a builder with
    /// <see cref="Components.Logging()"/>, change its properties with the methods of this class, and pass it
    /// to <see cref="ConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />.
    /// </para>
    /// <para>
    /// By default, the SDK has the following logging behavior:
    /// </para>
    /// <list type="bullet">
    /// <item><description> Log messages are written to standard output. To change this, use a log adapter as
    /// described in <see cref="Adapter(ILogAdapter)"/> and <see cref="Components.Logging(ILogAdapter)"/>. </description></item>
    /// <item><description> The lowest enabled log level is <see cref="LaunchDarkly.Logging.LogLevel.Info"/>,
    /// so <see cref="LaunchDarkly.Logging.LogLevel.Debug"/> messages are not shown. To change this, use
    /// <see cref="Level(LaunchDarkly.Logging.LogLevel)"/>. </description></item>
    /// <item><description> The base logger name is <c>LaunchDarkly.Sdk</c>. See <see cref="BaseLoggerName(string)"/>
    /// for more about logger names and how to change the name. </description></item>
    /// </list>
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
        private string _baseLoggerName = null;
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
        /// Specifies a custom base logger name.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Logger names are used to give context to the log output, indicating that it is from the
        /// LaunchDarkly SDK instead of another component, or indicating a more specific area of
        /// functionality within the SDK. The default console logging implementation shows the logger
        /// name in brackets, for instance:
        /// </para>
        /// <code>
        ///     [LaunchDarkly.Sdk.DataSource] INFO: Reconnected to LaunchDarkly stream
        /// </code>
        /// <para>
        /// If you are using an adapter for a third-party logging framework such as NUnit (see
        /// <see cref="Adapter(ILogAdapter)"/>), most frameworks have a mechanism for filtering log
        /// output by the logger name.
        /// </para>
        /// <para>
        /// By default, the SDK uses a base logger name of <c>LaunchDarkly.Sdk</c>. Messages will be
        /// logged either under this name, or with a suffix to indicate what general area of
        /// functionality is involved:
        /// </para>
        /// <list type="bullet">
        /// <item><description> <c>.DataSource</c>: problems or status messages regarding how the SDK gets
        /// feature flag data from LaunchDarkly. </description></item>
        /// <item><description> <c>.DataStore</c>: problems or status messages regarding how the SDK stores its
        /// feature flag data (for instance, if you are using a database). </description></item>
        /// <item><description> <c>.Evaluation</c>: problems in evaluating a feature flag or flags, which were
        /// caused by invalid flag data or incorrect usage of the SDK rather than for instance a
        /// database problem. </description></item>
        /// <item><description> <c>.Events</c> problems or status messages regarding the SDK's delivery of
        /// analytics event data to LaunchDarkly. </description></item>
        /// </list>
        /// <para>
        /// Setting <c>BaseLoggerName</c> to a non-null value overrides the default. The SDK still
        /// adds the same suffixes to the name, so for instance if you set it to <c>"LD"</c>, the
        /// example message above would show <c>[LD.DataSource]</c>.
        /// </para>
        /// </remarks>
        public LoggingConfigurationBuilder BaseLoggerName(string baseLoggerName)
        {
            _baseLoggerName = baseLoggerName;
            return this;
        }

        /// <summary>
        /// Specifies the implementation of logging to use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <a href="https://github.com/launchdarkly/dotnet-logging"><c>LaunchDarkly.Logging</c></a> API defines the
        /// <c>ILogAdapter</c> interface to specify where log output should be sent. By default, it is set to
        /// <c>Logs.ToConsole</c>, meaning that output will be sent to <c>Console.Error</c>. You may use other
        /// <c>LaunchDarkly.Logging.Logs</c> methods, or a custom implementation, to handle log output differently.
        /// For instance, in .NET Core, specify <c>Logs.CoreLogging</c> to use the standard .NET Core logging framework.
        /// </para>
        /// <para>
        /// For more about logging adapters, see the <a href="https://docs.launchdarkly.com/sdk/features/logging#net">SDK
        /// reference guide</a>, the <a href="https://launchdarkly.github.io/dotnet-logging/html/N_LaunchDarkly_Logging.htm">API
        /// documentation</a> for <c>LaunchDarkly.Logging</c>, and the
        /// <a href="https://github.com/launchdarkly/dotnet-logging-adapters">third-party adapters</a> that
        /// LaunchDarkly provides (you can also create your own adapter using the <c>LaunchDarkly.Logging</c> API).
        /// </para>
        /// <para>
        /// If you don't need to customize any options other than the adapter, you can call
        /// <see cref="Components.Logging(ILogAdapter)"/> as a shortcut rather than using
        /// <see cref="LoggingConfigurationBuilder"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        ///     // This example configures the SDK to use the standard .NET Core log framework
        ///     // (Microsoft.Extensions.Logging).
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().Adapter(Logs.CoreLogging(coreLoggingFactory)))
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
        public LoggingConfiguration CreateLoggingConfiguration()
        {
            ILogAdapter logAdapter;
            if (_logAdapter is null)
            {
                logAdapter = Logs.ToConsole.Level(_minimumLevel ?? LogLevel.Info);
            }
            else
            {
                logAdapter = _minimumLevel.HasValue ?
                   _logAdapter.Level(_minimumLevel.Value) :
                   _logAdapter;
            }
            return new LoggingConfiguration(
                _baseLoggerName,
                logAdapter,
                _logDataSourceOutageAsErrorAfter
                );
        }
    }
}

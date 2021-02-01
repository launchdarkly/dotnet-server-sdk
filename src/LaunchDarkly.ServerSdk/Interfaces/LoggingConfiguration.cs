using System;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Integrations;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Encapsulates the SDK's general logging configuration.
    /// </summary>
    public sealed class LoggingConfiguration
    {
        /// <summary>
        /// The configured base logger name, or <c>null</c> to use the default.
        /// </summary>
        /// <seealso cref="LoggingConfigurationBuilder.BaseLoggerName(string)"/>
        public string BaseLoggerName { get; }

        /// <summary>
        /// The implementation of logging that the SDK will use.
        /// </summary>
        /// <seealso cref="LoggingConfigurationBuilder.Adapter(ILogAdapter)"/>
        public ILogAdapter LogAdapter { get; }

        /// <summary>
        /// The time threshold, if any, after which the SDK will log a data source outage at <c>Error</c> level
        /// instead of <c>Warn</c> level.
        /// </summary>
        /// <seealso cref="LoggingConfigurationBuilder.LogDataSourceOutageAsErrorAfter(TimeSpan?)"/>
        public TimeSpan? LogDataSourceOutageAsErrorAfter { get; }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="baseLoggerName">value for <see cref="BaseLoggerName"/></param>
        /// <param name="logAdapter">value for <see cref="LogAdapter"/></param>
        /// <param name="logDataSourceOutageAsErrorAfter">value for <see cref="LogDataSourceOutageAsErrorAfter"/></param>
        public LoggingConfiguration(
            string baseLoggerName,
            ILogAdapter logAdapter,
            TimeSpan? logDataSourceOutageAsErrorAfter
            )
        {
            BaseLoggerName = baseLoggerName;
            LogAdapter = logAdapter ?? Logs.None;
            LogDataSourceOutageAsErrorAfter = logDataSourceOutageAsErrorAfter;
        }
    }
}

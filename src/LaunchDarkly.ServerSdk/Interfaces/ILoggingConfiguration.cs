using LaunchDarkly.Logging;

namespace LaunchDarkly.Sdk.Server.Interfaces
{
    /// <summary>
    /// Encapsulates the SDK's general logging configuration.
    /// </summary>
    public interface ILoggingConfiguration
    {
        /// <summary>
        /// The implementation of logging that the SDK will use.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The <a href="https://github.com/launchdarkly/dotnet-logging">LaunchDarkly.Logging</a> API defines the
        /// <c>ILogAdapter</c> interface to specify where log output should be sent. By default, it is set to
        /// <c>Logs.ToConsole.Level(LogLevel.Info)</c>, meaning that output will be sent to <c>Console.Error</c>
        /// and that it will not include any <c>Debug</c>-level logging.
        /// </para>
        /// <para>
        /// SDK components should not use this property directly; instead, the SDK client will use it to create a
        /// logger instance which will be in <see cref="LdClientContext"/>.
        /// </para>
        /// </remarks>
        ILogAdapter LogAdapter { get; }
    }
}

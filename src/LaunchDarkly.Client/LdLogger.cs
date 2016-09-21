using Microsoft.Extensions.Logging;

namespace LaunchDarkly.Client
{
    internal static class LdLogger
    {
        internal static ILoggerFactory LoggerFactory = new LoggerFactory();

        internal static ILogger CreateLogger<T>()
        {
            return LoggerFactory.CreateLogger<T>();
        }

        internal static ILogger CreateLogger(string categoryName)
        {
            return LoggerFactory.CreateLogger(categoryName);
        }
    }
}

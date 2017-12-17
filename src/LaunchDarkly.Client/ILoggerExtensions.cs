using System;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.Client
{
    internal static class ILoggerExtensions
    {
        public static void LogDebug(this ILogger logger, 
            Exception exception, 
            string message, 
            params object[] args) {

            logger.LogDebug( 0, exception, message, args);
        }

        public static void LogWarning( this ILogger logger, 
            Exception exception, 
            string message, 
            params object[] args) {

            logger.LogWarning( 0, exception, message, args);
        }

        public static void LogError(this ILogger logger, 
            Exception exception, 
            string message, 
            params object[] args) {

            logger.LogError(0, exception, message, args);
        }
    }
}

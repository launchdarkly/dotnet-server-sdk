using System;

namespace LaunchDarkly.Client
{
    static class Util
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal static long GetUnixTimestampMillis(DateTime dateTime)
        {
            return (long) (dateTime - UnixEpoch).TotalMilliseconds;
        }

        internal static string ExceptionMessage(Exception e)
        {
            var msg = e.Message;
            if (e.InnerException != null)
            {
                return msg + " with inner excpetion: " + e.InnerException.Message;
            }
            return msg;
        }
    }
}

using System;
using Common.Logging;

namespace LaunchDarklyClient
{
	public static class Util
	{
		private static readonly ILog log = LogManager.GetLogger(nameof(Util));

		private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public static long GetUnixTimestampMillis(DateTime dateTime)
		{
			try
			{
				log.Trace($"Start {nameof(GetUnixTimestampMillis)}");

				return (long) (dateTime - unixEpoch).TotalMilliseconds;
			}
			finally
			{
				log.Trace($"End {nameof(GetUnixTimestampMillis)}");
			}
		}

		internal static string ExceptionMessage(Exception e)
		{
			try
			{
				log.Trace($"Start {nameof(ExceptionMessage)}");

				string msg = e.Message;
				return e.InnerException != null ? $"{msg} with inner exception: {e.InnerException.Message}" : msg;
			}
			finally
			{
				log.Trace($"End {nameof(ExceptionMessage)}");
			}
		}
	}
}
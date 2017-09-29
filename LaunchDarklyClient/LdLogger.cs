using Common.Logging;

namespace LaunchDarklyClient
{
	public static class LdLogger
	{
		public static ILogManager LogManager {get; set;}

		public static ILog CreateLogger<T>()
		{
			return LogManager.GetLogger(typeof(T).Name);
		}

		public static ILog CreateLogger(string name)
		{
			return LogManager.GetLogger(name);
		}
	}
}
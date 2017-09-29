using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient
{
	internal class Prerequisite
	{
		private static readonly ILog log = LogManager.GetLogger<Prerequisite>();

		[JsonConstructor]
		internal Prerequisite(string key, int variation)
		{
			try
			{
				log.Trace($"Start constructor {nameof(Prerequisite)}(string, int)");

				Key = key;
				Variation = variation;
			}
			finally
			{
				log.Trace($"End constructor {nameof(Prerequisite)}(string, int)");
			}
		}

		internal string Key {get;}
		internal int Variation {get;}
	}
}
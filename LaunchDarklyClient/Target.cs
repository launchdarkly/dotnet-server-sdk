using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient
{
	internal class Target
	{
		private static readonly ILog log = LogManager.GetLogger<Target>();

		[JsonConstructor]
		internal Target(List<string> values, int variation)
		{
			try
			{
				log.Trace($"Start constructor {nameof(Target)}(List<string>, int)");

				Values = values;
				Variation = variation;
			}
			finally
			{
				log.Trace($"End constructor {nameof(Target)}(List<string>, int)");
			}
		}

		internal List<string> Values {get;}
		internal int Variation {get;}
	}
}
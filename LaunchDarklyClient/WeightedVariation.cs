using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient
{
	internal class WeightedVariation
	{
		private static readonly ILog log = LogManager.GetLogger<WeightedVariation>();

		[JsonConstructor]
		internal WeightedVariation(int variation, int weight)
		{
			try
			{
				log.Trace($"Start constructor {nameof(WeightedVariation)}(int, int)");

				Variation = variation;
				Weight = weight;
			}
			finally
			{
				log.Trace($"End constructor {nameof(WeightedVariation)}(int, int)");
			}
		}

		internal int Variation {get;}
		internal int Weight {get;}
	}
}
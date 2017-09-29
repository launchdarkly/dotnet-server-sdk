using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient
{
	internal class Rollout
	{
		private static readonly ILog log = LogManager.GetLogger<Rollout>();

		[JsonConstructor]
		internal Rollout(List<WeightedVariation> variations, string bucketBy)
		{
			try
			{
				log.Trace($"Start constructor {nameof(Rollout)}(List<WeightedVariation>, string)");

				Variations = variations;
				BucketBy = bucketBy;
			}
			finally
			{
				log.Trace($"End constructor {nameof(Rollout)}(List<WeightedVariation>, string)");
			}
		}

		internal List<WeightedVariation> Variations {get;}
		internal string BucketBy {get;}
	}
}
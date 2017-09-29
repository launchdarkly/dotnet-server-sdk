using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient
{
	internal class VariationOrRollout
	{
		private static readonly ILog log = LogManager.GetLogger<VariationOrRollout>();

		private const float LongScale = 0xFFFFFFFFFFFFFFFL;

		[JsonConstructor]
		internal VariationOrRollout(int? variation, Rollout rollout)
		{
			try
			{
				log.Trace($"Start constructor {nameof(VariationOrRollout)}(int?, Rollout)");

				Variation = variation;
				Rollout = rollout;
			}
			finally
			{
				log.Trace($"End constructor {nameof(VariationOrRollout)}(int?, Rollout)");
			}
		}

		internal int? Variation {get;}
		internal Rollout Rollout {get;}

		internal int? VariationIndexForUser(User user, string key, string salt)
		{
			try
			{
				log.Trace($"Start {nameof(VariationIndexForUser)}");

				if (Variation.HasValue)
				{
					return Variation.Value;
				}

				if (Rollout != null)
				{
					string bucketBy = Rollout.BucketBy ?? "key";
					float bucket = BucketUser(user, key, bucketBy, salt);
					float sum = 0F;
					foreach (WeightedVariation variation in Rollout.Variations)
					{
						sum += variation.Weight / 100000F;
						if (bucket < sum)
						{
							return variation.Variation;
						}
					}
				}
				return null;
			}
			finally
			{
				log.Trace($"End {nameof(VariationIndexForUser)}");
			}
		}

		private float BucketUser(User user, string featureKey, string attr, string salt)
		{
			try
			{
				log.Trace($"Start {nameof(BucketUser)}");

				JToken userValue = user.GetValueForEvaluation(attr);
				if (userValue != null && userValue.Type.Equals(JTokenType.String))
				{
					string idHash = userValue.Value<string>();
					if (!string.IsNullOrEmpty(user.SecondaryKey))
					{
						idHash += "." + user.SecondaryKey;
					}

					string hash = Hash($"{featureKey}.{salt}.{idHash}").Substring(0, 15);
					long longValue = long.Parse(hash, NumberStyles.HexNumber);
					return longValue / LongScale;
				}

				return 0F;
			}
			finally
			{
				log.Trace($"End {nameof(BucketUser)}");
			}
		}

		private static string Hash(string s)
		{
			SHA1 sha = SHA1.Create();
			try
			{
				log.Trace($"Start {nameof(Hash)}");

				byte[] data = sha.ComputeHash(Encoding.UTF8.GetBytes(s));

				StringBuilder sb = new StringBuilder();
				foreach (byte t in data)
				{
					sb.Append(t.ToString("x2"));
				}

				return sb.ToString();
			}
			finally
			{
				sha.Clear();
				log.Trace($"End {nameof(Hash)}");
			}
		}
	}
}
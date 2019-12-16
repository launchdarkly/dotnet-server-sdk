using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    internal class VariationOrRollout
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        [JsonProperty(PropertyName = "variation")]
        internal int? Variation { get; private set; }
        [JsonProperty(PropertyName = "rollout")]
        internal Rollout Rollout { get; private set; }

        [JsonConstructor]
        internal VariationOrRollout(int? variation, Rollout rollout)
        {
            Variation = variation;
            Rollout = rollout;
        }

        internal int? VariationIndexForUser(User user, string key, string salt)
        {
            if (Variation.HasValue)
            {
                return Variation.Value;
            }

            if (Rollout != null && Rollout.Variations != null && Rollout.Variations.Count > 0)
            {
                string bucketBy = Rollout.BucketBy ?? "key";
                float bucket = BucketUser(user, key, bucketBy, salt);
                float sum = 0F;
                foreach (WeightedVariation wv in Rollout.Variations)
                {
                    sum += (float) wv.Weight / 100000F;
                    if (bucket < sum)
                    {
                        return wv.Variation;
                    }
                }
                // The user's bucket value was greater than or equal to the end of the last bucket. This could happen due
                // to a rounding error, or due to the fact that we are scaling to 100000 rather than 99999, or the flag
                // data could contain buckets that don't actually add up to 100000. Rather than returning an error in
                // this case (or changing the scaling, which would potentially change the results for *all* users), we
                // will simply put the user in the last bucket.
                return Rollout.Variations[Rollout.Variations.Count - 1].Variation;
            }
            return null;
        }

        internal static float BucketUser(User user, string featureKey, string attr, string salt)
        {
            var idHash = BucketableStringValue(Operator.GetUserAttributeForEvaluation(user, attr));
            if (idHash != null)
            {
                if (!string.IsNullOrEmpty(user.SecondaryKey))
                    idHash += "." + user.SecondaryKey;

                var hash = Hash(String.Format("{0}.{1}.{2}", featureKey, salt, idHash)).Substring(0, 15);
                var longValue = long.Parse(hash, NumberStyles.HexNumber);
                return longValue / longScale;
            }

            return 0F;
        }

        private static string BucketableStringValue(LdValue value)
        {
            if (!value.IsNull)
            {
                if (value.IsString)
                {
                    return value.AsString;
                }
                if (value.IsInt)
                {
                    return Convert.ToString(value.AsInt);
                }
            }
            return null;
        }

        private static string Hash(string s)
        {
            var sha = SHA1.Create();
            byte[] data = sha.ComputeHash(Encoding.UTF8.GetBytes(s));

            var sb = new StringBuilder();
            foreach (byte t in data)
                sb.Append(t.ToString("x2"));

            return sb.ToString();
        }
    }
}
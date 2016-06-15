using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    public class VariationOrRollout
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public int? Variation { get; set; }

        [JsonProperty(PropertyName = "rollout", NullValueHandling = NullValueHandling.Ignore)]
        public Rollout Rollout { get; set; }


        internal int? VariationIndexForUser(User user, string key, string salt)
        {
            if (Variation.HasValue)
            {
                return Variation.Value;
            }

            if (Rollout != null)
            {
                string bucketBy = Rollout.BucketBy == null ? "key" : Rollout.BucketBy;
                float bucket = BucketUser(user, key, bucketBy, salt);
                float sum = 0F;
                foreach (WeightedVariation wv in Rollout.Variations)
                {
                    sum += (float)wv.Weight / 100000F;
                    if (bucket < sum)
                    {
                        return wv.Variation;
                    }
                }
            }
            return null;
        }

        private float BucketUser(User user, string featureKey, string attr, string salt)
        {
            var userValue = user.getValueForEvaluation(attr);
            if (userValue != null && userValue.Type.Equals(JTokenType.String))
            {
                var idHash = userValue.Value<string>();
                if (!string.IsNullOrEmpty(user.SecondaryKey))
                    idHash += "." + user.SecondaryKey;

                var hash = ShaHex.Hash($"{featureKey}.{salt}.{idHash}").Substring(0, 15);
                var longValue = long.Parse(hash, NumberStyles.HexNumber);
                return longValue / longScale;
            }

            return 0F;
        }
    }
}
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    class VariationOrRollout
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        internal int? Variation { get; private set; }
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
            var userValue = user.GetValueForEvaluation(attr);
            if (userValue != null && userValue.Type.Equals(JTokenType.String))
            {
                var idHash = userValue.Value<string>();
                if (!string.IsNullOrEmpty(user.SecondaryKey))
                    idHash += "." + user.SecondaryKey;

                var hash = Hash(String.Format("{0}.{1}.{2}", featureKey, salt, idHash)).Substring(0, 15);
                var longValue = long.Parse(hash, NumberStyles.HexNumber);
                return longValue / longScale;
            }

            return 0F;
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
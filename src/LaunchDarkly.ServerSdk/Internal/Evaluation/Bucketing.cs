using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    internal static class Bucketing
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        internal static float ComputeBucketValue(
            bool isExperiment,
            int? seed,
            in Context context,
            in ContextKind? contextKind,
            string key,
            in AttributeRef? attr,
            string salt
            )
        {
            if (!context.TryGetContextByKind(contextKind ?? ContextKind.Default, out var matchContext))
            {
                return 0;
            }

            LdValue contextValue;
            if (isExperiment || !attr.HasValue || !attr.Value.Defined) // always bucket by key in an experiment
            {
                contextValue = LdValue.Of(matchContext.Key);
            }
            else
            {
                if (!attr.Value.Valid)
                {
                    return 0;
                }
                contextValue = matchContext.GetValue(attr.Value);
                if (contextValue.IsNull)
                {
                    return 0;
                }
            }

            var hashInputBuilder = new StringBuilder(100);
            if (seed.HasValue)
            {
                hashInputBuilder.Append(seed.Value);
            }
            else
            {
                hashInputBuilder.Append(key).Append(".").Append(salt);
            }
            hashInputBuilder.Append(".");
            if (contextValue.IsString)
            {
                hashInputBuilder.Append(contextValue.AsString);
            }
            else if (contextValue.IsInt)
            {
                hashInputBuilder.Append(contextValue.AsInt);
            }
            else
            {
                return 0; // bucket-by values other than strings and ints aren't supported
            }
            if (!isExperiment)  // secondary key is not supported in experiments
            {
                var secondary = matchContext.Secondary;
                if (!(secondary is null))
                {
                    hashInputBuilder.Append(".").Append(secondary);
                }
            }
            var hash = Hash(hashInputBuilder.ToString()).Substring(0, 15);
            var longValue = long.Parse(hash, NumberStyles.HexNumber);
            return longValue / longScale;
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

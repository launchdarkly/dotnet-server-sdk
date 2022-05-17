using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    internal static class Bucketing
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        internal static float BucketContext(int? seed, in Context context, string key, in AttributeRef? attr, string salt)
        {
            var contextValue = (attr.HasValue && attr.Value.Defined) ? context.GetValue(attr.Value) : LdValue.Of(context.Key);
            if (contextValue.IsNull)
            {
                return 0; // attribute not found
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
            var secondary = context.Secondary;
            if (!string.IsNullOrEmpty(secondary))
            {
                hashInputBuilder.Append(".").Append(secondary);
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

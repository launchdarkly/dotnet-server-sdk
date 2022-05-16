using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    internal static class Bucketing
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        internal static float BucketUser(int? seed, User user, string key, string bucketBy, string salt)
        {
            var userValue = string.IsNullOrEmpty(bucketBy) ? LdValue.Of(user.Key) : user.GetAttribute(UserAttribute.ForName(bucketBy));
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
            if (userValue.IsString)
            {
                hashInputBuilder.Append(userValue.AsString);
            }
            else if (userValue.IsInt)
            {
                hashInputBuilder.Append(userValue.AsInt);
            }
            else
            {
                return 0; // bucket-by values other than strings and ints aren't supported
            }
            var secondary = user.Secondary;
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

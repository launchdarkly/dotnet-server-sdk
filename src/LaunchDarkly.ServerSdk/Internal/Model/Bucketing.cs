using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal static class Bucketing
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        internal static float BucketUser(User user, string featureKey, string attr, string salt)
        {
            var idHash = BucketableStringValue(Operator.GetUserAttributeForEvaluation(user, attr));
            if (idHash != null)
            {
                if (!string.IsNullOrEmpty(user.Secondary))
                    idHash += "." + user.Secondary;

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

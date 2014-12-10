using System.Security.Cryptography;
using System.Text;

namespace LaunchDarkly.Client
{
    public class ShaHex
    {
        public static string Hash(string s)
        {
            var sha = SHA1.Create();
            byte[] data = sha.ComputeHash(Encoding.Default.GetBytes(s));
            
            var sb = new StringBuilder();
            foreach (byte t in data)
                sb.Append(t.ToString("x2"));

            return sb.ToString();
        }
    }
}

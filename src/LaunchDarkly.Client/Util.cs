using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace LaunchDarkly.Client
{
    internal static class Util
    {
        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        internal static readonly string Version = ((AssemblyInformationalVersionAttribute)typeof(LdClient)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))
            .InformationalVersion;

        public static Dictionary<string, string> GetRequestHeaders(IBaseConfiguration config)
        {
            return new Dictionary<string, string> {
                { "Authorization", config.SdkKey },
                { "User-Agent", config.UserAgentType + "/" + Util.Version }
            };
        }

        public static HttpClient MakeHttpClient(IBaseConfiguration config)
        {
            var httpClient = new HttpClient(handler: config.HttpClientHandler, disposeHandler: false);
            foreach (var h in GetRequestHeaders(config))
            {
                httpClient.DefaultRequestHeaders.Add(h.Key, h.Value);
            }
            return httpClient;
        }

        public static long GetUnixTimestampMillis(DateTime dateTime)
        {
            return (long) (dateTime - UnixEpoch).TotalMilliseconds;
        }

        internal static string ExceptionMessage(Exception e)
        {
            var msg = e.Message;
            if (e.InnerException != null)
            {
                return msg + " with inner exception: " + e.InnerException.Message;
            }
            return msg;
        }
    }
}
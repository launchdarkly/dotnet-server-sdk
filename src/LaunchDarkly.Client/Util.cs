using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace LaunchDarkly.Client
{
    internal static class Util
    {
        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        public static Dictionary<string, string> GetRequestHeaders(IBaseConfiguration config,
            ClientEnvironment env)
        {
            return new Dictionary<string, string> {
                { "Authorization", config.SdkKey },
                { "User-Agent", env.UserAgentType + "/" + env.Version }
            };
        }

        public static HttpClient MakeHttpClient(IBaseConfiguration config, ClientEnvironment env)
        {
            var httpClient = new HttpClient(handler: config.HttpClientHandler, disposeHandler: false);
            foreach (var h in GetRequestHeaders(config, env))
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
using System;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    internal class JsonUtil
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };

        // Wrapper for JsonConvert.DeserializeObject that ensures we use consistent settings and minimizes our Newtonsoft references.
        internal static T DecodeJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
        }

        // Wrapper for JsonConvert.DeserializeObject that ensures we use consistent settings and minimizes our Newtonsoft references.
        internal static object DecodeJson(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type, _jsonSettings);
        }

        // Wrapper for JsonConvert.SerializeObject that ensures we use consistent settings and minimizes our Newtonsoft references.
        internal static string EncodeJson(object o)
        {
            return JsonConvert.SerializeObject(o, _jsonSettings);
        }
    }
}

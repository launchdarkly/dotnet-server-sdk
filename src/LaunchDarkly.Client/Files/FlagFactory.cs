using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client.Files
{
    internal static class FlagFactory
    {
        public static IVersionedData FlagFromJson(JToken json)
        {
            return json.ToObject(VersionedDataKind.Features.GetItemType()) as IVersionedData;
        }

        public static IVersionedData FlagWithValue(string key, JToken value)
        {
            var o = new JObject();
            o.Add("key", key);
            o.Add("on", true);
            var vs = new JArray();
            vs.Add(value);
            o.Add("variations", vs);
            var ft = new JObject();
            ft.Add("variation", 0);
            o.Add("fallthrough", ft);
            return FlagFromJson(o);
        }

        public static IVersionedData SegmentFromJson(JToken json)
        {
            return json.ToObject(VersionedDataKind.Segments.GetItemType()) as IVersionedData;
        }
    }
}

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client.Files
{
    // Represents the data structure that we parse files into, and provides the logic for
    // transferring its contents into the format used by the data store.
    class FlagFileData
    {
        [JsonProperty(PropertyName = "flags", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> Flags { get; set; }

        [JsonProperty(PropertyName = "flagValues", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> FlagValues { get; set; }

        [JsonProperty(PropertyName = "segments", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> Segments { get; set; }

        public void AddToData(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            if (Flags != null)
            {
                foreach (KeyValuePair<string, JToken> e in Flags)
                {
                    AddItem(allData, VersionedDataKind.Features, FlagFactory.FlagFromJson(e.Value));
                }
            }
            if (FlagValues != null)
            {
                foreach (KeyValuePair<string, JToken> e in FlagValues)
                {
                    AddItem(allData, VersionedDataKind.Features, FlagFactory.FlagWithValue(e.Key, e.Value));
                }
            }
            if (Segments != null)
            {
                foreach (KeyValuePair<string, JToken> e in Segments)
                {
                    AddItem(allData, VersionedDataKind.Segments, FlagFactory.SegmentFromJson(e.Value));
                }
            }
        }

        private void AddItem(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData,
            IVersionedDataKind kind, IVersionedData item)
        {
            IDictionary<string, IVersionedData> items;
            if (!allData.TryGetValue(kind, out items))
            {
                items = new Dictionary<string, IVersionedData>();
                allData[kind] = items;
            }
            if (items.ContainsKey(item.Key))
            {
                throw new System.Exception("in \"" + kind.GetNamespace() + "\", key \"" + item.Key +
                    "\" was already defined");
            }
            items[item.Key] = item;
        }
    }
}

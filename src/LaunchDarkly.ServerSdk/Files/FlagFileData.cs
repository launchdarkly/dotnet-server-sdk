using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Files
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

        public void AddToData(IDictionary<string, ItemDescriptor> flagsOut, IDictionary<string, ItemDescriptor> segmentsOut)
        {
            if (Flags != null)
            {
                foreach (KeyValuePair<string, JToken> e in Flags)
                {
                    AddItem(DataKinds.Features, flagsOut, e.Key, FlagFactory.FlagFromJson(e.Value));
                }
            }
            if (FlagValues != null)
            {
                foreach (KeyValuePair<string, JToken> e in FlagValues)
                {
                    AddItem(DataKinds.Features, flagsOut, e.Key, FlagFactory.FlagWithValue(e.Key, e.Value));
                }
            }
            if (Segments != null)
            {
                foreach (KeyValuePair<string, JToken> e in Segments)
                {
                    AddItem(DataKinds.Segments, segmentsOut, e.Key, FlagFactory.SegmentFromJson(e.Value));
                }
            }
        }

        private void AddItem(DataKind kind, IDictionary<string, ItemDescriptor> items, string key, object item)
        {
            if (items.ContainsKey(key))
            {
                throw new System.Exception("in \"" + kind.Name + "\", key \"" + key +
                    "\" was already defined");
            }
            items[key] = new ItemDescriptor(1, item);
        }
    }
}

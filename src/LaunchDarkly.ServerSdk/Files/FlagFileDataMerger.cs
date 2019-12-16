using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace LaunchDarkly.Client.Files
{
    // Represents the data structure that we parse files into, and provides the logic for
    // transferring its contents into the format used by the feature store.
    internal sealed class FlagFileDataMerger
    {
        private readonly DuplicateKeysHandling _duplicateKeysHandling;

        public FlagFileDataMerger(DuplicateKeysHandling duplicateKeysHandling)
        {
            _duplicateKeysHandling = duplicateKeysHandling;
        }

        public void AddToData(FlagFileData data, IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            if (data.Flags != null)
            {
                foreach (KeyValuePair<string, JToken> e in data.Flags)
                {
                    AddItem(allData, VersionedDataKind.Features, FlagFactory.FlagFromJson(e.Value));
                }
            }
            if (data.FlagValues != null)
            {
                foreach (KeyValuePair<string, JToken> e in data.FlagValues)
                {
                    AddItem(allData, VersionedDataKind.Features, FlagFactory.FlagWithValue(e.Key, e.Value));
                }
            }
            if (data.Segments != null)
            {
                foreach (KeyValuePair<string, JToken> e in data.Segments)
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
                switch (_duplicateKeysHandling)
                {
                    case DuplicateKeysHandling.Throw:
                        throw new System.Exception("in \"" + kind.GetNamespace() + "\", key \"" + item.Key +
                            "\" was already defined");
                    case DuplicateKeysHandling.Ignore:
                        break;
                    default:
                        throw new NotImplementedException("Unknown duplicate keys handling: " + _duplicateKeysHandling);
                }
            }
            else
            {
                items[item.Key] = item;
            }
        }
    }
}

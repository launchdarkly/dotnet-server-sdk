using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Newtonsoft.Json.Linq;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Files
{
    // Provides the logic for merging sets of feature flag and segment data.
    internal sealed class FlagFileDataMerger
    {
        private readonly DuplicateKeysHandling _duplicateKeysHandling;

        public FlagFileDataMerger(DuplicateKeysHandling duplicateKeysHandling)
        {
            _duplicateKeysHandling = duplicateKeysHandling;
        }

        public void AddToData(FlagFileData data, IDictionary<string, ItemDescriptor> flagsOut, IDictionary<string, ItemDescriptor> segmentsOut)
        {
            if (data.Flags != null)
            {
                foreach (KeyValuePair<string, JToken> e in data.Flags)
                {
                    AddItem(DataKinds.Features, flagsOut, e.Key, FlagFactory.FlagFromJson(e.Value));
                }
            }
            if (data.FlagValues != null)
            {
                foreach (KeyValuePair<string, JToken> e in data.FlagValues)
                {
                    AddItem(DataKinds.Features, flagsOut, e.Key, FlagFactory.FlagWithValue(e.Key, e.Value));
                }
            }
            if (data.Segments != null)
            {
                foreach (KeyValuePair<string, JToken> e in data.Segments)
                {
                    AddItem(DataKinds.Segments, segmentsOut, e.Key, FlagFactory.SegmentFromJson(e.Value));
                }
            }
        }

        private void AddItem(DataKind kind, IDictionary<string, ItemDescriptor> items, string key, object item)
        {
            if (items.ContainsKey(key))
            {
                switch (_duplicateKeysHandling)
                {
                    case DuplicateKeysHandling.Throw:
                        throw new System.Exception("in \"" + kind.Name + "\", key \"" + key +
                            "\" was already defined");
                    case DuplicateKeysHandling.Ignore:
                        break;
                    default:
                        throw new NotImplementedException("Unknown duplicate keys handling: " + _duplicateKeysHandling);
                }
            }
            else
            {
                items[key] = new ItemDescriptor(1, item);
            }
        }
    }
}

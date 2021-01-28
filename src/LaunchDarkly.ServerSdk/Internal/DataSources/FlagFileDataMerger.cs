using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    // Provides the logic for merging sets of feature flag and segment data.
    internal sealed class FlagFileDataMerger
    {
        private readonly FileDataTypes.DuplicateKeysHandling _duplicateKeysHandling;

        public FlagFileDataMerger(FileDataTypes.DuplicateKeysHandling duplicateKeysHandling)
        {
            _duplicateKeysHandling = duplicateKeysHandling;
        }

        public void AddToData(FlagFileData data, IDictionary<string, ItemDescriptor> flagsOut, IDictionary<string, ItemDescriptor> segmentsOut)
        {
            if (data.Flags != null)
            {
                foreach (KeyValuePair<string, FeatureFlag> e in data.Flags)
                {
                    AddItem(DataModel.Features, flagsOut, e.Key, e.Value);
                }
            }
            if (data.FlagValues != null)
            {
                foreach (KeyValuePair<string, LdValue> e in data.FlagValues)
                {
                    AddItem(DataModel.Features, flagsOut, e.Key, FlagFactory.FlagWithValue(e.Key, e.Value));
                }
            }
            if (data.Segments != null)
            {
                foreach (KeyValuePair<string, Segment> e in data.Segments)
                {
                    AddItem(DataModel.Segments, segmentsOut, e.Key, e.Value);
                }
            }
        }

        private void AddItem(DataKind kind, IDictionary<string, ItemDescriptor> items, string key, object item)
        {
            if (items.ContainsKey(key))
            {
                switch (_duplicateKeysHandling)
                {
                    case FileDataTypes.DuplicateKeysHandling.Throw:
                        throw new System.Exception("in \"" + kind.Name + "\", key \"" + key +
                            "\" was already defined");
                    case FileDataTypes.DuplicateKeysHandling.Ignore:
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

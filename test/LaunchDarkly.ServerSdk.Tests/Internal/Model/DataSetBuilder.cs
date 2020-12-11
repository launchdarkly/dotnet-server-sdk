using System.Collections.Generic;
using System.Linq;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;
using static LaunchDarkly.Sdk.Server.TestUtils;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal class DataSetBuilder
    {
        private readonly Dictionary<string, FeatureFlag> _flags =
            new Dictionary<string, FeatureFlag>();
        private readonly Dictionary<string, Segment> _segments =
            new Dictionary<string, Segment>();

        internal DataSetBuilder Flags(params FeatureFlag[] flags)
        {
            foreach (var flag in flags)
            {
                _flags[flag.Key] = flag;
            }
            return this;
        }

        internal DataSetBuilder Segments(params Segment[] segments)
        {
            foreach (var segment in segments)
            {
                _segments[segment.Key] = segment;
            }
            return this;
        }

        internal DataSetBuilder RemoveFlag(string key)
        {
            _flags.Remove(key);
            return this;
        }

        internal DataSetBuilder RemoveSegment(string key)
        {
            _segments.Remove(key);
            return this;
        }

        internal FullDataSet<ItemDescriptor> Build()
        {
            return new FullDataSet<ItemDescriptor>(
                new Dictionary<DataKind, KeyedItems<ItemDescriptor>>
                {
                    {
                        DataKinds.Features,
                        new KeyedItems<ItemDescriptor>(
                            _flags.ToDictionary(kv => kv.Key, kv => DescriptorOf(kv.Value))
                        )
                    },
                    {
                        DataKinds.Segments,
                        new KeyedItems<ItemDescriptor>(
                            _segments.ToDictionary(kv => kv.Key, kv => DescriptorOf(kv.Value))
                        )
                    },
                }
            );
        }
    }
}

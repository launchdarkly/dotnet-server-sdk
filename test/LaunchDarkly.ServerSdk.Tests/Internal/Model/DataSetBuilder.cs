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

        public static FullDataSet<ItemDescriptor> Empty => new DataSetBuilder().Build();

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
                        DataModel.Features,
                        new KeyedItems<ItemDescriptor>(
                            _flags.ToDictionary(kv => kv.Key, kv => DescriptorOf(kv.Value))
                        )
                    },
                    {
                        DataModel.Segments,
                        new KeyedItems<ItemDescriptor>(
                            _segments.ToDictionary(kv => kv.Key, kv => DescriptorOf(kv.Value))
                        )
                    },
                }
            );
        }
    }

    public static class DataSetExtensions
    {
        public static string ToJsonString(this FullDataSet<ItemDescriptor> data)
        {
            var ob0 = LdValue.BuildObject();
            foreach (var kv0 in data.Data)
            {
                if (kv0.Key == DataModel.Features || kv0.Key == DataModel.Segments)
                {
                    var ob1 = LdValue.BuildObject();
                    foreach (var kv1 in kv0.Value.Items)
                    {
                        ob1.Add(kv1.Key, LdValue.Parse(kv0.Key.Serialize(kv1.Value)));
                    }
                    ob0.Add(kv0.Key == DataModel.Features ? "flags" : "segments", ob1.Build());
                }
            }
            return ob0.Build().ToJsonString();
        }

        internal static string ToJsonString(this FeatureFlag item) => DataModel.Features.Serialize(DescriptorOf(item));

        internal static string ToJsonString(this Segment item) => DataModel.Segments.Serialize(DescriptorOf(item));
    }
}

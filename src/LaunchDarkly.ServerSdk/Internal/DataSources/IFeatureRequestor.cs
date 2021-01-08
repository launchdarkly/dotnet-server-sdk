using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Newtonsoft.Json;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal interface IFeatureRequestor : IDisposable
    {
        Task<AllData> GetAllDataAsync();
        Task<FeatureFlag> GetFlagAsync(string featureKey);
        Task<Segment> GetSegmentAsync(string segmentKey);
    }
    
    internal class AllData
    {
        internal IDictionary<string, FeatureFlag> Flags { get; private set; }

        internal IDictionary<string, Segment> Segments { get; private set; }

        [JsonConstructor]
        internal AllData(IDictionary<string, FeatureFlag> flags, IDictionary<string, Segment> segments)
        {
            Flags = flags;
            Segments = segments;
        }

        internal FullDataSet<ItemDescriptor> ToInitData()
        {
            return new FullDataSet<ItemDescriptor>(ImmutableList.Create(
                new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(DataModel.Features,
                    new KeyedItems<ItemDescriptor>(Flags.ToImmutableDictionary(kv => kv.Key, kv => new ItemDescriptor(kv.Value.Version, kv.Value)))),
                new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(DataModel.Segments,
                    new KeyedItems<ItemDescriptor>(Segments.ToImmutableDictionary(kv => kv.Key, kv => new ItemDescriptor(kv.Value.Version, kv.Value))))
            ));
        }
    }
}

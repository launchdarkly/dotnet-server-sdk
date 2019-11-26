using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Server.Interfaces;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server
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

        internal IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> ToGenericDictionary()
        {
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> ret =
                new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
            // sadly the following is necessary because IDictionary has invariant type parameters...
            IDictionary<string, IVersionedData> items = new Dictionary<string, IVersionedData>();
            foreach (var entry in Flags)
            {
                items[entry.Key] = entry.Value;
            }
            ret.Add(VersionedDataKind.Features, items);
            items = new Dictionary<string, IVersionedData>();
            foreach (var entry in Segments)
            {
                items[entry.Key] = entry.Value;
            }
            ret.Add(VersionedDataKind.Segments, items);
            return ret;
        }
    }
}

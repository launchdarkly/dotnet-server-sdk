using System.Collections.Generic;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal sealed class SegmentRule
    {
        [JsonProperty(PropertyName = "clauses")]
        internal List<Clause> Clauses { get; private set; }
        [JsonProperty(PropertyName = "weight", NullValueHandling = NullValueHandling.Ignore)]
        internal int? Weight { get; private set; }
        [JsonProperty(PropertyName = "bucketBy", NullValueHandling = NullValueHandling.Ignore)]
        internal UserAttribute? BucketBy { get; private set; }

        [JsonConstructor]
        internal SegmentRule(List<Clause> clauses, int? weight, UserAttribute? bucketBy)
        {
            Clauses = clauses ?? new List<Clause>();
            Weight = weight;
            BucketBy = bucketBy;
        }

        internal SegmentRule()
        {
        }
    }
}

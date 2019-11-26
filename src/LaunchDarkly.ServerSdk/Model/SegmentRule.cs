using System.Collections.Generic;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Model
{
    internal class SegmentRule
    {
        [JsonProperty(PropertyName = "clauses")]
        internal List<Clause> Clauses { get; private set; }
        [JsonProperty(PropertyName = "weight")]
        internal int? Weight { get; private set; }
        [JsonProperty(PropertyName = "bucketBy")]
        internal string BucketBy { get; private set; }

        [JsonConstructor]
        internal SegmentRule(List<Clause> clauses, int? weight, string bucketBy)
        {
            Clauses = clauses;
            Weight = weight;
            BucketBy = bucketBy;
        }

        internal SegmentRule()
        {
        }
    }
}

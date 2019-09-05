using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
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

        public bool MatchesUser(User user, string segmentKey, string salt)
        {
            foreach (var c in Clauses)
            {
                if (!c.MatchesUserNoSegments(user))
                {
                    return false;
                }
            }

            // If the Weight is absent, this rule matches
            if (!Weight.HasValue)
            {
                return true;
            }

            // All of the clauses are met. See if the user buckets in
            String by = (BucketBy == null) ? "key" : BucketBy;
            double bucket = VariationOrRollout.BucketUser(user, segmentKey, by, salt);
            double weight = (double)this.Weight / 100000F;
            return bucket < weight;
        }
    }
}

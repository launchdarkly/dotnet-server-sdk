using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.JsonStream;
using LaunchDarkly.Sdk.Json;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    [JsonStreamConverter(typeof(SegmentSerialization))]
    internal sealed class Segment : IJsonSerializable
    {
        public string Key { get; }
        public int Version { get; }
        public bool Deleted { get; }
        internal IEnumerable<string> Included { get; }
        internal IEnumerable<string> Excluded { get; }
        internal IEnumerable<SegmentRule> Rules { get; }
        internal string Salt { get; }
        internal bool Unbounded { get; }
        internal int? Generation { get; }
        internal PreprocessedData Preprocessed { get; }

        internal Segment(
            string key,
            int version,
            bool deleted,
            IEnumerable<string> included,
            IEnumerable<string> excluded,
            IEnumerable<SegmentRule> rules,
            string salt,
            bool unbounded,
            int? generation
            )
        {
            Key = key;
            Version = version;
            Deleted = deleted;
            Included = included ?? Enumerable.Empty<string>();
            Excluded = excluded ?? Enumerable.Empty<string>();
            Rules = rules ?? Enumerable.Empty<SegmentRule>();
            Salt = salt;
            Unbounded = unbounded;
            Generation = generation;
            Preprocessed = Preprocess(Included, Excluded);
        }

        private static PreprocessedData Preprocess(IEnumerable<string> included, IEnumerable<string> excluded) =>
            new PreprocessedData
            {
                IncludedSet = included.ToImmutableHashSet(),
                ExcludedSet = excluded.ToImmutableHashSet()
            };

        internal struct PreprocessedData
        {
            internal ImmutableHashSet<string> IncludedSet { get; set; }
            internal ImmutableHashSet<string> ExcludedSet { get; set; }
        }
    }

    internal struct SegmentRule
    {
        internal IEnumerable<Clause> Clauses { get; }
        internal int? Weight { get; }
        internal UserAttribute? BucketBy { get; }

        internal SegmentRule(IEnumerable<Clause> clauses, int? weight, UserAttribute? bucketBy)
        {
            Clauses = clauses ?? Enumerable.Empty<Clause>();
            Weight = weight;
            BucketBy = bucketBy;
        }
    }
}

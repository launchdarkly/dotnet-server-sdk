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
        internal ImmutableList<string> Included { get; }
        internal ImmutableList<string> Excluded { get; }
        internal ImmutableList<SegmentTarget> IncludedContexts { get; }
        internal ImmutableList<SegmentTarget> ExcludedContexts { get; }
        internal IEnumerable<SegmentRule> Rules { get; }
        internal string Salt { get; }
        internal bool Unbounded { get; }
        internal string UnboundedContextKind { get; }
        internal int? Generation { get; }
        internal PreprocessedData Preprocessed { get; }

        internal Segment(
            string key,
            int version,
            bool deleted,
            IEnumerable<string> included,
            IEnumerable<string> excluded,
            IEnumerable<SegmentTarget> includedContexts,
            IEnumerable<SegmentTarget> excludedContexts,
            IEnumerable<SegmentRule> rules,
            string salt,
            bool unbounded,
            string unboundedContextKind,
            int? generation
            )
        {
            Key = key;
            Version = version;
            Deleted = deleted;
            Included = included is null ? ImmutableList.Create<string>() : included.ToImmutableList();
            Excluded = excluded is null ? ImmutableList.Create<string>() : excluded.ToImmutableList();
            IncludedContexts = includedContexts is null ? ImmutableList.Create<SegmentTarget>() : includedContexts.ToImmutableList();
            ExcludedContexts = excludedContexts is null ? ImmutableList.Create<SegmentTarget>() : excludedContexts.ToImmutableList();
            Rules = rules ?? Enumerable.Empty<SegmentRule>();
            Salt = salt;
            Unbounded = unbounded;
            UnboundedContextKind = unboundedContextKind;
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

    internal readonly struct SegmentTarget
    {
        internal readonly string ContextKind;
        internal readonly IEnumerable<string> Values;
        internal readonly ImmutableHashSet<string> PreprocessedValues;

        public SegmentTarget(string contextKind, IEnumerable<string> values)
        {
            ContextKind = contextKind;
            Values = values ?? Enumerable.Empty<string>();
            PreprocessedValues = Values.ToImmutableHashSet();
        }
    }

    internal struct SegmentRule
    {
        internal IEnumerable<Clause> Clauses { get; }
        internal int? Weight { get; }
        internal string RolloutContextKind { get; }
        internal string BucketBy { get; }

        internal SegmentRule(IEnumerable<Clause> clauses, int? weight, string rolloutContextKind, string bucketBy)
        {
            Clauses = clauses ?? Enumerable.Empty<Clause>();
            Weight = weight;
            RolloutContextKind = rolloutContextKind;
            BucketBy = bucketBy;
        }
    }
}

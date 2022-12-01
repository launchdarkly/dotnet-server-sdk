using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using LaunchDarkly.Sdk.Json;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    [JsonConverter(typeof(FeatureFlagSerialization))]
    internal sealed class FeatureFlag : IJsonSerializable
    {
        internal string Key { get; }
        internal int Version { get; }
        internal bool Deleted { get; }
        internal bool On { get; }
        internal ImmutableList<Prerequisite> Prerequisites { get; }
        internal ImmutableList<Target> Targets { get; }
        internal ImmutableList<Target> ContextTargets { get; }
        internal ImmutableList<FlagRule> Rules { get; }
        internal VariationOrRollout Fallthrough { get; }
        internal int? OffVariation { get; }
        internal IEnumerable<LdValue> Variations { get; }
        internal string Salt { get; }
        public bool TrackEvents { get; }
        public bool TrackEventsFallthrough { get; }
        public UnixMillisecondTime? DebugEventsUntilDate { get; private set; }
        public bool ClientSide { get; set; }

        internal FeatureFlag(string key, int version, bool deleted, bool on, IEnumerable<Prerequisite> prerequisites,
            ImmutableList<Target> targets, ImmutableList<Target> contextTargets, IEnumerable<FlagRule> rules, VariationOrRollout fallthrough, int? offVariation,
            IEnumerable<LdValue> variations, string salt, bool trackEvents, bool trackEventsFallthrough, UnixMillisecondTime? debugEventsUntilDate,
            bool clientSide)
        {
            Key = key;
            Version = version;
            Deleted = deleted;
            On = on;
            Prerequisites = prerequisites is null ? ImmutableList.Create<Prerequisite>() : prerequisites.ToImmutableList();
            Targets = targets is null ? ImmutableList.Create<Target>() : targets.ToImmutableList();
            ContextTargets = contextTargets is null ? ImmutableList.Create<Target>() : contextTargets.ToImmutableList();
            Rules = rules is null ? ImmutableList.Create<FlagRule>() : rules.ToImmutableList();
            Fallthrough = fallthrough;
            OffVariation = offVariation;
            Variations = variations ?? Enumerable.Empty<LdValue>();
            Salt = salt;
            TrackEvents = trackEvents;
            TrackEventsFallthrough = trackEventsFallthrough;
            DebugEventsUntilDate = debugEventsUntilDate;
            ClientSide = clientSide;
        }
    }

    internal struct Rollout
    {
        internal RolloutKind Kind { get; }
        internal ContextKind? ContextKind { get; }
        internal int? Seed { get; }
        internal IEnumerable<WeightedVariation> Variations { get; }
        internal AttributeRef BucketBy { get; }

        internal Rollout(RolloutKind kind, ContextKind? contextKind, int? seed, IEnumerable<WeightedVariation> variations, AttributeRef bucketBy)
        {
            Kind = kind;
            ContextKind = contextKind;
            Seed = seed;
            Variations = variations ?? Enumerable.Empty<WeightedVariation>();
            BucketBy = bucketBy;
        }
    }

    internal enum RolloutKind
    {
        Rollout,
        Experiment
    }

    internal struct VariationOrRollout
    {
        internal int? Variation { get; }
        internal Rollout? Rollout { get; }

        internal VariationOrRollout(int? variation, Rollout? rollout)
        {
            Variation = variation;
            Rollout = rollout;
        }
    }

    internal struct WeightedVariation
    {
        internal int Variation { get; }
        internal int Weight { get; }
        internal bool Untracked { get; }

        internal WeightedVariation(int variation, int weight, bool untracked)
        {
            Variation = variation;
            Weight = weight;
            Untracked = untracked;
        }
    }

    internal struct Target
    {
        internal ContextKind? ContextKind { get; }
        internal IEnumerable<string> Values { get; }
        internal int Variation { get; }
        internal PreprocessedData Preprocessed { get; }

        internal Target(ContextKind? contextKind, IEnumerable<string> values, int variation)
        {
            ContextKind = contextKind;
            Values = values ?? Enumerable.Empty<string>();
            Variation = variation;
            Preprocessed = Preprocess(Values);
        }

        private static PreprocessedData Preprocess(IEnumerable<string> values) =>
            new PreprocessedData
            {
                ValuesSet = values.ToImmutableHashSet()
            };

        internal struct PreprocessedData
        {
            internal ImmutableHashSet<string> ValuesSet { get; set; }
        }
    }

    internal struct Prerequisite
    {
        internal string Key { get; }
        internal int Variation { get; }

        internal Prerequisite(string key, int variation)
        {
            Key = key;
            Variation = variation;
        }
    }

    internal struct FlagRule
    {
        internal int? Variation { get; }
        internal Rollout? Rollout { get; }
        internal string Id { get; }
        internal IEnumerable<Clause> Clauses { get; }
        internal bool TrackEvents { get; }

        internal FlagRule(int? variation, Rollout? rollout, string id, IEnumerable<Clause> clauses, bool trackEvents)
        {
            Variation = variation;
            Rollout = rollout;
            Id = id;
            Clauses = clauses ?? Enumerable.Empty<Clause>();
            TrackEvents = trackEvents;
        }
    }

    class EvaluationException : Exception
    {
        public EvaluationException(string message)
            : base(message)
        {
        }
    }
}

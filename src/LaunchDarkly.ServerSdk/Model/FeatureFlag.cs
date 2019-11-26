using System;
using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Model
{
    internal class FeatureFlag : IVersionedData
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FeatureFlag));

        [JsonProperty(PropertyName = "key")]
        public string Key { get; private set; }
        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }
        [JsonProperty(PropertyName = "on")]
        internal bool On { get; private set; }
        [JsonProperty(PropertyName = "prerequisites")]
        internal List<Prerequisite> Prerequisites { get; private set; }
        [JsonProperty(PropertyName = "salt")]
        internal string Salt { get; private set; }
        [JsonProperty(PropertyName = "targets")]
        internal List<Target> Targets { get; private set; }
        [JsonProperty(PropertyName = "rules")]
        internal List<Rule> Rules { get; private set; }
        [JsonProperty(PropertyName = "fallthrough")]
        internal VariationOrRollout Fallthrough { get; private set; }
        [JsonProperty(PropertyName = "offVariation")]
        internal int? OffVariation { get; private set; }
        [JsonProperty(PropertyName = "variations")]
        internal List<LdValue> Variations { get; private set; }
        [JsonProperty(PropertyName = "trackEvents")]
        public bool TrackEvents { get; private set; }
        [JsonProperty(PropertyName = "trackEventsFallthrough")]
        public bool TrackEventsFallthrough { get; private set; }
        [JsonProperty(PropertyName = "debugEventsUntilDate")]
        public long? DebugEventsUntilDate { get; private set; }
        [JsonProperty(PropertyName = "deleted")]
        public bool Deleted { get; set; }
        [JsonProperty(PropertyName = "clientSide")]
        public bool ClientSide { get; set; }

        [JsonConstructor]
        internal FeatureFlag(string key, int version, bool on, List<Prerequisite> prerequisites, string salt,
            List<Target> targets, List<Rule> rules, VariationOrRollout fallthrough, int? offVariation,
            List<LdValue> variations, bool trackEvents, bool trackEventsFallthrough, long? debugEventsUntilDate,
            bool deleted, bool clientSide)
        {
            Key = key;
            Version = version;
            On = on;
            Prerequisites = prerequisites;
            Salt = salt;
            Targets = targets;
            Rules = rules;
            Fallthrough = fallthrough;
            OffVariation = offVariation;
            Variations = variations;
            TrackEvents = trackEvents;
            TrackEventsFallthrough = trackEventsFallthrough;
            DebugEventsUntilDate = debugEventsUntilDate;
            Deleted = deleted;
            ClientSide = clientSide;
        }

        internal FeatureFlag()
        {
        }

        internal FeatureFlag(string key, int version, bool deleted)
        {
            Key = key;
            Version = version;
            Deleted = deleted;
        }
    }

    internal class Rollout
    {
        [JsonProperty(PropertyName = "variations")]
        internal List<WeightedVariation> Variations { get; private set; }
        [JsonProperty(PropertyName = "bucketBy")]
        internal string BucketBy { get; private set; }

        [JsonConstructor]
        internal Rollout(List<WeightedVariation> variations, string bucketBy)
        {
            Variations = variations;
            BucketBy = bucketBy;
        }
    }

    internal class WeightedVariation
    {
        [JsonProperty(PropertyName = "variation")]
        internal int Variation { get; private set; }
        [JsonProperty(PropertyName = "weight")]
        internal int Weight { get; private set; }

        [JsonConstructor]
        internal WeightedVariation(int variation, int weight)
        {
            Variation = variation;
            Weight = weight;
        }
    }

    internal class Target
    {
        [JsonProperty(PropertyName = "values")]
        internal List<string> Values { get; private set; }
        [JsonProperty(PropertyName = "variation")]
        internal int Variation { get; private set; }

        [JsonConstructor]
        internal Target(List<string> values, int variation)
        {
            Values = values;
            Variation = variation;
        }
    }

    internal class Prerequisite
    {
        [JsonProperty(PropertyName = "key")]
        internal string Key { get; private set; }
        [JsonProperty(PropertyName = "variation")]
        internal int Variation { get; private set; }

        [JsonConstructor]
        internal Prerequisite(string key, int variation)
        {
            Key = key;
            Variation = variation;
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
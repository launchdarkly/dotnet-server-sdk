
using System;
using System.Collections.Generic;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    public class FeatureFlag
    {
        private static readonly ILog Logger = LogProvider.For<FeatureFlag>();

        internal string Key { get; }
        internal int Version { get; set; }
        internal bool On { get; }
        internal List<Prerequisite> Prerequisites { get; }
        internal string Salt { get; }
        internal List<Target> Targets { get; }
        internal List<Rule> Rules { get; }
        internal VariationOrRollout Fallthrough { get; }
        internal int? OffVariation { get; }
        internal List<JToken> Variations { get; }
        internal bool Deleted { get; set; }

        [JsonConstructor]
        public FeatureFlag(string key, int version, bool on, List<Prerequisite> prerequisites, string salt, List<Target> targets, List<Rule> rules, VariationOrRollout fallthrough, int? offVariation, List<JToken> variations, bool deleted)
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
            Deleted = deleted;
        }

        public FeatureFlag()
        {
        }

        internal struct EvalResult
        {
            internal JToken Value;
            internal readonly IList<FeatureRequestEvent> PrerequisiteEvents;
            internal readonly ISet<string> VisitedFeatureKeys;

            public EvalResult(JValue value, IList<FeatureRequestEvent> events, ISet<string> visited) : this()
            {
                Value = value;
                PrerequisiteEvents = events;
                VisitedFeatureKeys = visited;
            }
        }


        internal EvalResult? Evaluate(User user, IFeatureStore featureStore)
        {
            if (user == null || user.Key == null)
            {
                return null;
            }
            IList<FeatureRequestEvent> prereqEvents = new List<FeatureRequestEvent>();
            ISet<string> visited = new HashSet<string>();
            return Evaluate(user, featureStore, prereqEvents, visited);
        }

        // Returning either a nil EvalResult or EvalResult.value indicates prereq failure/error.
        private EvalResult? Evaluate(User user, IFeatureStore featureStore, IList<FeatureRequestEvent> events, ISet<string> visited)
        {
            var prereqOk = true;
            var evalResult = new EvalResult(null, events, visited);
            foreach (var prereq in Prerequisites)
            {
                evalResult.VisitedFeatureKeys.Add(Key);
                if (evalResult.VisitedFeatureKeys.Contains(prereq.Key))
                {
                    Logger.Error("Prerequisite cycle detected when evaluating feature flag: " + Key);
                    return null;
                }
                JToken prereqEvalResultValue = null;
                var prereqFeatureFlag = featureStore.Get(prereq.Key);
                if (prereqFeatureFlag == null)
                {
                    Logger.Error("Could not retrieve prerequisite flag: " + prereq.Key + " when evaluating: " + Key);
                    return null;
                }
                else if (prereqFeatureFlag.On)
                {
                    var prereqEvalResult = prereqFeatureFlag.Evaluate(user, featureStore, evalResult.PrerequisiteEvents, evalResult.VisitedFeatureKeys);
                    if (!prereqEvalResult.HasValue || prereqEvalResult.Value.Value == null || !prereqEvalResult.Value.Value.Equals(prereqFeatureFlag.GetVariation(prereq.Variation)))
                    {
                        prereqOk = false;
                    }
                    prereqEvalResultValue = prereqEvalResult?.Value;
                }
                else
                {
                    prereqOk = false;
                }
                //We don't short circuit and also send events for each prereq.
                evalResult.PrerequisiteEvents.Add(new FeatureRequestEvent(prereqFeatureFlag.Key, user, prereqEvalResultValue, null));
            }
            if (prereqOk)
            {
                evalResult.Value = GetVariation(EvaluateIndex(user));
            }
            return evalResult;
        }

        private int? EvaluateIndex(User user)
        {
            // Check to see if targets match
            foreach (var target in Targets)
            {
                foreach (var v in target.Values)
                {
                    if (v.Equals(user.Key))
                    {
                        return target.Variation;
                    }
                }
            }

            // Now walk through the rules and see if any match
            foreach (Rule rule in Rules)
            {
                if (rule.MatchesUser(user))
                {
                    return rule.VariationIndexForUser(user, Key, Salt);
                }
            }

            // Walk through the fallthrough and see if it matches
            return Fallthrough.VariationIndexForUser(user, Key, Salt);
        }

        private JToken GetVariation(int? index)
        {
            if (index == null || index >= Variations.Count)
            {
                return null;
            }
            else
            {
                return Variations[index.Value];
            }
        }

        internal JToken OffVariationValue
        {
            get
            {
                if (OffVariation.HasValue && OffVariation.Value < Variations.Count)
                {
                    return Variations[OffVariation.Value];
                }
                return null;
            }
        }
    }

    public class Rollout
    {
        internal List<WeightedVariation> Variations { get; }
        internal string BucketBy { get; }

        [JsonConstructor]
        public Rollout(List<WeightedVariation> variations, string bucketBy)
        {
            Variations = variations;
            BucketBy = bucketBy;
        }
    }

    public class WeightedVariation
    {
        internal int Variation { get; }
        internal int Weight { get; }

        [JsonConstructor]
        public WeightedVariation(int variation, int weight)
        {
            Variation = variation;
            Weight = weight;
        }
    }

    public class Rule : VariationOrRollout
    {
        internal List<Clause> Clauses { get; }

        [JsonConstructor]
        public Rule(int? variation, Rollout rollout, List<Clause> clauses) : base(variation, rollout)
        {
            Clauses = clauses;
        }

        internal bool MatchesUser(User user)
        {
            foreach (var c in Clauses)
            {
                if (!c.MatchesUser(user))
                {
                    return false;
                }

            }
            return true;
        }
    }

    public class Target
    {
        internal List<string> Values { get; }
        internal int Variation { get; }

        [JsonConstructor]
        public Target(List<string> values, int variation)
        {
            Values = values;
            Variation = variation;
        }
    }

    public class Prerequisite
    {
        internal string Key { get; }
        internal int Variation { get; }

        [JsonConstructor]
        public Prerequisite(string key, int variation)
        {
            Key = key;
            Variation = variation;
        }
    }
}

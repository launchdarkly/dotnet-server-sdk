
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
        [JsonProperty(PropertyName = "variations", NullValueHandling = NullValueHandling.Ignore)]
        public List<WeightedVariation> Variations { get; set; }

        [JsonProperty(PropertyName = "bucketBy", NullValueHandling = NullValueHandling.Ignore)]
        public string BucketBy { get; set; }
    }

    public class WeightedVariation
    {
        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public int Variation { get; set; }

        [JsonProperty(PropertyName = "weight", NullValueHandling = NullValueHandling.Ignore)]
        public int Weight { get; set; }

    }

    public class Rule : VariationOrRollout
    {
        [JsonProperty(PropertyName = "clauses", NullValueHandling = NullValueHandling.Ignore)]
        public List<Clause> Clauses { get; set; }

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
        [JsonProperty(PropertyName = "values", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Values { get; set; }

        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public int Variation { get; set; }
    }

    public class Prerequisite
    {
        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public int Variation { get; set; }
    }
}

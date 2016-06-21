using System.Collections.Generic;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    class FeatureFlag
    {
        private static readonly ILog Logger = LogProvider.For<FeatureFlag>();

        internal string Key { get; private set; }
        internal int Version { get; set; }
        internal bool On { get; private set; }
        internal List<Prerequisite> Prerequisites { get; private set; }
        internal string Salt { get; private set; }
        internal List<Target> Targets { get; private set; }
        internal List<Rule> Rules { get; private set; }
        internal VariationOrRollout Fallthrough { get; private set; }
        internal int? OffVariation { get; private set; }
        internal List<JToken> Variations { get; private set; }
        internal bool Deleted { get;  set; }

        [JsonConstructor]
        internal FeatureFlag(string key, int version, bool on, List<Prerequisite> prerequisites, string salt, List<Target> targets, List<Rule> rules, VariationOrRollout fallthrough, int? offVariation, List<JToken> variations, bool deleted)
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


        internal FeatureFlag()
        {
        }

        internal struct EvalResult
        {
            internal JToken Result;
            internal readonly IList<FeatureRequestEvent> PrerequisiteEvents;
            internal readonly ISet<string> VisitedFeatureKeys;

            internal EvalResult(JValue result, IList<FeatureRequestEvent> events, ISet<string> visited) : this()
            {
                Result = result;
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
                    if (!prereqEvalResult.HasValue || prereqEvalResult.Value.Result == null || !prereqEvalResult.Value.Result.Equals(prereqFeatureFlag.GetVariation(prereq.Variation)))
                    {
                        prereqOk = false;
                    }
                    if (prereqEvalResult.HasValue)
                    {
                        prereqEvalResultValue = prereqEvalResult.Value.Result;
                    }
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
                evalResult.Result = GetVariation(EvaluateIndex(user));
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

    class Rollout
    {
        internal List<WeightedVariation> Variations { get; private set; }
        internal string BucketBy { get; private set; }

        [JsonConstructor]
        internal Rollout(List<WeightedVariation> variations, string bucketBy)
        {
            Variations = variations;
            BucketBy = bucketBy;
        }
    }

    class WeightedVariation
    {
        internal int Variation { get; private set; }
        internal int Weight { get; private set; }

        [JsonConstructor]
        internal WeightedVariation(int variation, int weight)
        {
            Variation = variation;
            Weight = weight;
        }
    }

    class Target
    {
        internal List<string> Values { get; private set; }
        internal int Variation { get; private set; }

        [JsonConstructor]
        internal Target(List<string> values, int variation)
        {
            Values = values;
            Variation = variation;
        }
    }

    class Prerequisite
    {
        internal string Key { get; private set; }
        internal int Variation { get; private set; }

        [JsonConstructor]
        internal Prerequisite(string key, int variation)
        {
            Key = key;
            Variation = variation;
        }
    }
}

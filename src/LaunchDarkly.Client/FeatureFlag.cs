
using System;
using System.Collections.Generic;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace LaunchDarkly.Client
{
    public class FeatureFlag
    {
        private static readonly ILog Logger = LogProvider.For<FeatureFlag>();

        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        public int Version { get; set; }

        [JsonProperty(PropertyName = "on", NullValueHandling = NullValueHandling.Ignore)]
        public bool On { get; set; }

        [JsonProperty(PropertyName = "prerequisites", NullValueHandling = NullValueHandling.Ignore)]
        public List<Prerequisite> Prerequisites { get; set; }

        [JsonProperty(PropertyName = "salt", NullValueHandling = NullValueHandling.Ignore)]
        public string Salt { get; set; }

        [JsonProperty(PropertyName = "targets", NullValueHandling = NullValueHandling.Ignore)]
        public List<Target> Targets { get; set; }

        [JsonProperty(PropertyName = "rules", NullValueHandling = NullValueHandling.Ignore)]
        public List<Rule> Rules { get; set; }

        [JsonProperty(PropertyName = "fallthrough", NullValueHandling = NullValueHandling.Ignore)]
        public VariationOrRollout Fallthrough { get; set; }

        [JsonProperty(PropertyName = "offVariation", NullValueHandling = NullValueHandling.Ignore)]
        public int? OffVariation { get; set; }

        [JsonProperty(PropertyName = "variations", NullValueHandling = NullValueHandling.Ignore)]
        public List<JToken> Variations { get; set; }

        [JsonProperty(PropertyName = "deleted", NullValueHandling = NullValueHandling.Ignore)]
        public bool Deleted { get; set; }

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
        public String BucketBy { get; set; }
    }

    public class WeightedVariation
    {
        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public Int32 Variation { get; set; }

        [JsonProperty(PropertyName = "weight", NullValueHandling = NullValueHandling.Ignore)]
        public Int32 Weight { get; set; }

    }

    public class VariationOrRollout
    {
        private static readonly float longScale = 0xFFFFFFFFFFFFFFFL;

        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public int? Variation { get; set; }

        [JsonProperty(PropertyName = "rollout", NullValueHandling = NullValueHandling.Ignore)]
        public Rollout Rollout { get; set; }


        internal int? VariationIndexForUser(User user, String key, String salt)
        {
            if (Variation.HasValue)
            {
                return Variation.Value;
            }

            if (Rollout != null)
            {
                string bucketBy = Rollout.BucketBy == null ? "key" : Rollout.BucketBy;
                float bucket = BucketUser(user, key, bucketBy, salt);
                float sum = 0F;
                foreach (WeightedVariation wv in Rollout.Variations)
                {
                    sum += (float)wv.Weight / 100000F;
                    if (bucket < sum)
                    {
                        return wv.Variation;
                    }
                }
            }
            return null;
        }

        private float BucketUser(User user, String featureKey, String attr, String salt)
        {
            var userValue = user.getValueForEvaluation(attr);
            if (userValue != null && userValue.Type.Equals(JTokenType.String))
            {
                var idHash = userValue.Value<string>();
                if (!string.IsNullOrEmpty(user.SecondaryKey))
                    idHash += "." + user.SecondaryKey;

                var hash = ShaHex.Hash($"{featureKey}.{salt}.{idHash}").Substring(0, 15);
                var longValue = long.Parse(hash, NumberStyles.HexNumber);
                return longValue / longScale;
            }

            return 0F;
        }
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

    public class Clause
    {
        private static readonly ILog Logger = LogProvider.For<Clause>();

        [JsonProperty(PropertyName = "attribute", NullValueHandling = NullValueHandling.Ignore)]
        public String Attribute { get; set; }

        [JsonProperty(PropertyName = "op", NullValueHandling = NullValueHandling.Ignore)]
        public String Op { get; set; }

        [JsonProperty(PropertyName = "values", NullValueHandling = NullValueHandling.Ignore)]
        public List<JValue> Values { get; set; }

        [JsonProperty(PropertyName = "negate", NullValueHandling = NullValueHandling.Ignore)]
        public Boolean Negate { get; set; }


        internal bool MatchesUser(User user)
        {
            var userValue = user.getValueForEvaluation(Attribute);
            if (userValue == null)
            {
                return false;
            }

            if (userValue is JArray)
            {
                var array = userValue as JArray;
                foreach (var element in array)
                {
                    if (!(element is JValue))
                    {
                        Logger.Error("Invalid custom attribute value in user object: " + element);
                        return false;
                    }
                    if (MatchAny(element as JValue))
                    {
                        return MaybeNegate(true);
                    }
                }
                return MaybeNegate(false);
            }
            else if (userValue is JValue)
            {
                return MaybeNegate(MatchAny(userValue as JValue));
            }
            Logger.Warn("Got unexpected user attribute type: " + userValue.Type + " for user key: " + user.Key + " and attribute: " + Attribute);
            return false;
        }

        private bool MatchAny(JValue userValue)
        {
            foreach (var v in Values)
            {
                if (Operator.Apply(Op, userValue, v))
                {
                    return true;
                }
            }
            return false;
        }

        private bool MaybeNegate(bool b)
        {
            if (Negate)
            {
                return !b;
            }
            else
            {
                return b;
            }
        }

    }

    public class Target
    {
        [JsonProperty(PropertyName = "values", NullValueHandling = NullValueHandling.Ignore)]
        public List<String> Values { get; set; }

        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public Int32 Variation { get; set; }
    }

    public class Prerequisite
    {
        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public String Key { get; set; }

        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        public Int32 Variation { get; set; }
    }
}

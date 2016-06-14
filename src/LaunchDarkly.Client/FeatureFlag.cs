
using System;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
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
            internal JToken value;
            internal List<FeatureRequestEvent> prerequisiteEvents;
            internal ISet<string> visitedFeatureKeys;
            private object p;
            private IList<FeatureRequestEvent> events;
            private ISet<string> visited;

            public EvalResult(object p, IList<FeatureRequestEvent> events, ISet<string> visited) : this()
            {
                this.p = p;
                this.events = events;
                this.visited = visited;
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
            return evaluate(user, featureStore, prereqEvents, visited);
        }

        // Returning either a nil EvalResult or EvalResult.value indicates prereq failure/error.
        private EvalResult? evaluate(User user, IFeatureStore featureStore, IList<FeatureRequestEvent> events, ISet<string> visited)
        {
            var prereqOk = true;
            var evalResult = new EvalResult(null, events, visited);
            foreach (var prereq in Prerequisites)
            {
                evalResult.visitedFeatureKeys.Add(Key);
                if (evalResult.visitedFeatureKeys.Contains(prereq.Key))
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
                    var prereqEvalResult = prereqFeatureFlag.evaluate(user, featureStore, evalResult.prerequisiteEvents, evalResult.visitedFeatureKeys);
                    if (!prereqEvalResult.HasValue || prereqEvalResult.Value.value == null || !prereqEvalResult.Value.value.Equals(prereqFeatureFlag.getVariation(prereq.Variation)))
                    {
                        prereqOk = false;
                    }
                    prereqEvalResultValue = prereqEvalResult?.value;
                }
                else
                {
                    prereqOk = false;
                }
                //We don't short circuit and also send events for each prereq.
                evalResult.prerequisiteEvents.Add(new FeatureRequestEvent(prereqFeatureFlag.Key, user, prereqEvalResultValue, null));
            }
            if (prereqOk)
            {
                evalResult.value = getVariation(evaluateIndex(user));
            }
            return evalResult;
        }

        private int? evaluateIndex(User user)
        {
            // Check to see if targets match
            foreach (Target target in Targets)
            {
                foreach (string v in target.Values)
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
                if (rule.matchesUser(user))
                {
                    return rule.variationIndexForUser(user, Key, Salt);
                }
            }

            // Walk through the fallthrough and see if it matches
            return Fallthrough.variationIndexForUser(user, Key, Salt);
        }

        private JToken getVariation(int? index)
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
        public String bucketBy { get; set; }
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
        public Rollout rollout { get; set; }


        internal int? variationIndexForUser(User user, String key, String salt)
        {
            if (Variation.HasValue)
            {
                return Variation.Value;
            }

            if (rollout != null)
            {
                string bucketBy = rollout.bucketBy == null ? "key" : rollout.bucketBy;
                float bucket = bucketUser(user, key, bucketBy, salt);
                float sum = 0F;
                foreach (WeightedVariation wv in rollout.Variations)
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

        private float bucketUser(User user, String featureKey, String attr, String salt)
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

        internal bool matchesUser(User user)
        {
            foreach (var c in Clauses)
            {
                if (!c.matchesUser(user))
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
        public List<JToken> Values { get; set; }

        [JsonProperty(PropertyName = "negate", NullValueHandling = NullValueHandling.Ignore)]
        public Boolean Negate { get; set; }


        internal bool matchesUser(User user)
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
                    if (matchAny(element as JValue))
                    {
                        return maybeNegate(true);
                    }
                }
                return maybeNegate(false);
            }
            else if (userValue is JValue)
            {
                return maybeNegate(matchAny(userValue as JValue));
            }
            Logger.Warn("Got unexpected user attribute type: " + userValue.Type + " for user key: " + user.Key + " and attribute: " + Attribute);
            return false;
        }

        private bool matchAny(JValue userValue)
        {
            foreach (JValue v in Values)
            {
                if (Operator.apply(Op, userValue, v))
                {
                    return true;
                }
            }
            return false;
        }

        private bool maybeNegate(bool b)
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

    public class Operator
    {
        internal static bool apply(string op, Object uValue, Object cValue)
        {
            switch (op)
            {
                case "in":
                    if (uValue.Equals(cValue))
                        return true;
                    break;
                case "endsWith":
                case "startsWith":
                case "matches":
                case "contains":
                case "lessThan":
                case "lessThanOrEqual":
                case "greaterThan":
                case "greaterThanOrEqual":
                case "before":
                case "after":
                default: return false;
            }

            return false;
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



    public class TargetRule
    {
        [JsonProperty(PropertyName = "attribute", NullValueHandling = NullValueHandling.Ignore)]
        public string Attribute { get; set; }
        [JsonProperty(PropertyName = "op", NullValueHandling = NullValueHandling.Ignore)]
        public string Op { get; set; }
        [JsonProperty(PropertyName = "values", NullValueHandling = NullValueHandling.Ignore)]
        public List<Object> Values { get; set; }

        public bool Matches(User user)
        {
            var userValue = GetUserValue(user);

            if (!(userValue is string) && typeof(IEnumerable).IsAssignableFrom(userValue.GetType()))
            {
                var uvs = (IEnumerable<object>)userValue;
                return Values.Intersect<object>(uvs).Any();
            }
            foreach (object value in Values)
            {
                if (value == null || userValue == null)
                {
                    return false;
                }
                if (value.Equals(userValue))
                    return true;
                else
                {
                    double userValueDouble;
                    double valueDouble;
                    if (Double.TryParse(userValue.ToString(), out userValueDouble) && Double.TryParse(value.ToString(), out valueDouble))
                        if (userValueDouble.Equals(valueDouble)) return true;
                }
            }
            return false;
        }

        private Object GetUserValue(User user)
        {
            switch (Attribute)
            {
                case "key":
                    return user.Key;
                case "ip":
                    return user.IpAddress;
                case "country":
                    return user.Country;
                case "firstName":
                    return user.FirstName;
                case "lastName":
                    return user.LastName;
                case "avatar":
                    return user.Avatar;
                case "anonymous":
                    return user.Anonymous;
                case "name":
                    return user.Name;
                case "email":
                    return user.Email;
                default:
                    var token = user.Custom[Attribute];
                    if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                    {
                        var arr = (JArray)token;
                        return arr.Values<JToken>().Select(i => ((JValue)i).Value);
                    }
                    else if (token.Type == JTokenType.Object)
                    {
                        throw new ArgumentException(string.Format("Rule contains nested custom object for attribute '{0}'"), Attribute);
                    }
                    else
                    {
                        var val = (JValue)token;
                        return val.Value;
                    }
            }
        }
    }
}

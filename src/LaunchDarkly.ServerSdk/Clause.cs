using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    internal class Clause
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Clause));

        [JsonProperty(PropertyName = "attribute")]
        internal string Attribute { get; private set; }
        [JsonProperty(PropertyName = "op")]
        internal string Op { get; private set; }
        [JsonProperty(PropertyName = "values")]
        internal List<JValue> Values { get; private set; }
        [JsonProperty(PropertyName = "negate")]
        internal bool Negate { get; private set; }

        [JsonConstructor]
        internal Clause(string attribute, string op, List<JValue> values, bool negate)
        {
            Attribute = attribute;
            Op = op;
            Values = values;
            Negate = negate;
        }

        internal bool MatchesUser(User user, IFeatureStore store)
        {
            if (Op == "segmentMatch")
            {
                foreach (var value in Values)
                {
                    Segment segment = store.Get(VersionedDataKind.Segments, value.Value<string>());
                    if (segment != null && segment.MatchesUser(user))
                    {
                        return MaybeNegate(true);
                    }
                }
                return MaybeNegate(false);
            }
            else
            {
                return MatchesUserNoSegments(user);
            }
        }

        internal bool MatchesUserNoSegments(User user)
        {
            var userValue = user.GetValueForEvaluation(Attribute);
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
                        Log.ErrorFormat("Invalid custom attribute value in user object: {0}",
                            element);
                        return false;
                    }
                    if (MatchAny(ExpressionValue.FromJsonValue(element as JValue)))
                    {
                        return MaybeNegate(true);
                    }
                }
                return MaybeNegate(false);
            }
            else if (userValue is JValue)
            {
                return MaybeNegate(MatchAny(ExpressionValue.FromJsonValue(userValue as JValue)));
            }
            Log.WarnFormat("Got unexpected user attribute type: {0} for user key: {1} and attribute: {2}",
                userValue.Type,
                user.Key,
                Attribute);
            return false;
        }

        // these static values let us avoid creating new JValue objects for true and false
        private static readonly JValue TrueValue = new JValue(true);
        private static readonly JValue FalseValue = new JValue(false);

        internal static ExpressionValue GetUserAttributeForEvaluation(User user, string attribute)
        {
            switch (attribute)
            {
                case "key":
                    return ExpressionValue.FromString(user.Key);
                case "secondary":
                    return ExpressionValue.FromString(user.SecondaryKey);
                case "ip":
                    return ExpressionValue.FromString(user.IPAddress);
                case "email":
                    return ExpressionValue.FromString(user.Email);
                case "avatar":
                    return ExpressionValue.FromString(user.Avatar);
                case "firstName":
                    return ExpressionValue.FromString(user.FirstName);
                case "lastName":
                    return ExpressionValue.FromString(user.LastName);
                case "name":
                    return ExpressionValue.FromString(user.Name);
                case "country":
                    return ExpressionValue.FromString(user.Country);
                case "anonymous":
                    if (user.Anonymous.HasValue)
                    {
                        return ExpressionValue.FromJsonValue(user.Anonymous.Value ? TrueValue : FalseValue);
                    }
                    return ExpressionValue.FromJsonValue(null);
                default:
                    JToken customValue;
                    user.Custom.TryGetValue(attribute, out customValue);
                    return ExpressionValue.FromJsonValue(customValue);
            }
        }

        private bool MatchAny(ExpressionValue userValue)
        {
            foreach (var v in Values)
            {
                if (Operator.Apply(Op, userValue, ExpressionValue.FromJsonValue(v)))
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
}
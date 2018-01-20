using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    internal class Clause
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<Clause>();

        internal string Attribute { get; private set; }
        internal string Op { get; private set; }
        internal List<JValue> Values { get; private set; }
        internal bool Negate { get; private set; }

        [JsonConstructor]
        internal Clause(string attribute, string op, List<JValue> values, bool negate)
        {
            Attribute = attribute;
            Op = op;
            Values = values;
            Negate = negate;
        }

        internal bool MatchesUser(User user, ISegmentStore segmentStore)
        {
            if (Op == "segmentMatch")
            {
                foreach (var value in Values)
                {
                    Segment segment = segmentStore.Get(value.Value<string>());
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
                        Logger.LogError("Invalid custom attribute value in user object: {0}",
                            element);
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
            Logger.LogWarning("Got unexpected user attribute type: {0} for user key: {1} and attribute: {2}",
                userValue.Type,
                user.Key,
                Attribute);
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
}
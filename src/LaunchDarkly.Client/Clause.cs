using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    internal class Clause
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<Clause>();

        internal string Attribute { get; }
        internal string Op { get; }
        internal List<JValue> Values { get; }
        internal bool Negate { get; }

        [JsonConstructor]
        internal Clause(string attribute, string op, List<JValue> values, bool negate)
        {
            Attribute = attribute;
            Op = op;
            Values = values;
            Negate = negate;
        }

        internal bool MatchesUser(User user, Configuration configuration)
        {
            JToken userValue = user.GetValueForEvaluation(Attribute);
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
                        Logger.LogError($"Invalid custom attribute value in user object: {element}");
                        return false;
                    }
                    if (MatchAny(element as JValue))
                    {
                        return MaybeNegate(true);
                    }
                }
                return MaybeNegate(false);
            }
            if (userValue is JValue)
            {
                return MaybeNegate(MatchAny(userValue as JValue));
            }
            Logger.LogWarning($"Got unexpected user attribute type: {userValue.Type} for user key: {user.Key} and attribute: {Attribute}");
            return false;
        }

        private bool MatchAny(JValue userValue)
        {
            foreach (JValue v in Values)
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
            return Negate ? !b : b;
        }
    }
}
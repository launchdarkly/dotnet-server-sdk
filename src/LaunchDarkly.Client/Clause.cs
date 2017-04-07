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
            object userValue = user.GetValueForEvaluation(Attribute);
            if (userValue == null )
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
                    if (MatchAny(element as JValue, configuration))
                    {
                        return MaybeNegate(true);
                    }
                }
                return MaybeNegate(false);
            }

            if (userValue is JValue)
            {
                return MaybeNegate(MatchAny(userValue as JValue, configuration));
            }

            return MaybeNegate(MatchAny(userValue, configuration)); 
        }

        private bool MatchAny(object userValue, Configuration configuration)
        {
            foreach (JValue clauseValue in Values)
            {
                if (Operator.Apply(Op, userValue, clauseValue, configuration))
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
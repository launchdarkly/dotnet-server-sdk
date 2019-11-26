using System.Collections.Generic;
using Common.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Model
{
    internal class Clause
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Clause));

        [JsonProperty(PropertyName = "attribute")]
        internal string Attribute { get; private set; }
        [JsonProperty(PropertyName = "op")]
        internal string Op { get; private set; }
        [JsonProperty(PropertyName = "values")]
        internal List<LdValue> Values { get; private set; }
        [JsonProperty(PropertyName = "negate")]
        internal bool Negate { get; private set; }

        [JsonConstructor]
        internal Clause(string attribute, string op, List<LdValue> values, bool negate)
        {
            Attribute = attribute;
            Op = op;
            Values = values;
            Negate = negate;
        }

        internal bool MatchesUser(User user, IDataStore store)
        {
            if (Op == "segmentMatch")
            {
                foreach (var value in Values)
                {
                    Segment segment = store.Get(VersionedDataKind.Segments, value.AsString);
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
            var userValue = Operator.GetUserAttributeForEvaluation(user, Attribute);
            if (userValue.IsNull)
            {
                return false;
            }
            if (userValue.Type == LdValueType.Array)
            {
                var list = userValue.AsList(LdValue.Convert.Json);
                foreach (var element in list)
                {
                    if (element.Type == LdValueType.Array || element.Type == LdValueType.Object)
                    {
                        Log.ErrorFormat("Invalid custom attribute value in user object: {0}",
                            element);
                        return false;
                    }
                    if (MatchAny(element))
                    {
                        return MaybeNegate(true);
                    }
                }
                return MaybeNegate(false);
            }
            else if (userValue.Type == LdValueType.Object)
            {
                Log.WarnFormat("Got unexpected user attribute type: {0} for user key: {1} and attribute: {2}",
                userValue.Type,
                user.Key,
                Attribute);
                return false;
            }
            else
            {
                return MaybeNegate(MatchAny(userValue));
            }
        }

        private bool MatchAny(LdValue userValue)
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
using System;
using System.Collections.Generic;
using LaunchDarkly.Client.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
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
}
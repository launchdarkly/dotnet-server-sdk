using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace LaunchDarkly.Client {
    internal class Clause
    {

        internal string Attribute { get; }
        internal string Op { get; }
        internal IList<object> Values { get; }
        internal bool Negate { get; }

        [JsonConstructor]
        internal Clause(string attribute, string op, IList<object> values, bool negate)
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

            if( !( userValue is string ) )
            {
                var eUserValue = userValue as IEnumerable;
                if(eUserValue != null)
                {
                    foreach (object value in eUserValue )
                    {
                        if (MatchAny(value, configuration))
                        {
                            return MaybeNegate(true);
                        }
                    }
                    return MaybeNegate(false);
                }
            }

            return MaybeNegate(MatchAny(userValue, configuration)); 
        }

        private bool MatchAny(object userValue, Configuration configuration)
        {
            foreach (object clauseValue in Values)
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
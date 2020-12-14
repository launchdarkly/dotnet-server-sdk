using System.Collections.Generic;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal sealed class Clause
    {
        [JsonProperty(PropertyName = "attribute")]
        internal UserAttribute Attribute { get; private set; }
        [JsonProperty(PropertyName = "op")]
        internal string Op { get; private set; }
        [JsonProperty(PropertyName = "values")]
        internal List<LdValue> Values { get; private set; }
        [JsonProperty(PropertyName = "negate")]
        internal bool Negate { get; private set; }

        [JsonConstructor]
        internal Clause(UserAttribute attribute, string op, List<LdValue> values, bool negate)
        {
            Attribute = attribute;
            Op = op;
            Values = values;
            Negate = negate;
        }
    }
}
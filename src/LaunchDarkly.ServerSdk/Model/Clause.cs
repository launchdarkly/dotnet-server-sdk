using System.Collections.Generic;
using Common.Logging;
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
    }
}
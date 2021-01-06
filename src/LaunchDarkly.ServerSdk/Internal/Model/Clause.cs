using System.Collections.Generic;
using System.Linq;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal struct Clause
    {
        internal UserAttribute Attribute { get; }
        internal string Op { get; }
        internal IEnumerable<LdValue> Values { get; }
        internal bool Negate { get; }

        internal Clause(UserAttribute attribute, string op, IEnumerable<LdValue> values, bool negate)
        {
            Attribute = attribute;
            Op = op;
            Values = values ?? Enumerable.Empty<LdValue>();
            Negate = negate;
        }
    }
}
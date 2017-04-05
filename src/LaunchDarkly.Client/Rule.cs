using System.Collections.Generic;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    internal class Rule : VariationOrRollout
    {
        internal List<Clause> Clauses { get; private set; }

        [JsonConstructor]
        internal Rule(int? variation, Rollout rollout, List<Clause> clauses) : base(variation, rollout)
        {
            Clauses = clauses;
        }

        internal bool MatchesUser(User user, Configuration configuration)
        {
            foreach (var c in Clauses)
            {
                if (!c.MatchesUser(user, configuration))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
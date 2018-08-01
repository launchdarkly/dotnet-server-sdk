using System.Collections.Generic;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    internal class Rule : VariationOrRollout
    {
        [JsonProperty(PropertyName = "clauses")]
        internal List<Clause> Clauses { get; private set; }

        [JsonConstructor]
        internal Rule(int? variation, Rollout rollout, List<Clause> clauses) : base(variation, rollout)
        {
            Clauses = clauses;
        }

        internal bool MatchesUser(User user, IFeatureStore store)
        {
            foreach (var c in Clauses)
            {
                if (!c.MatchesUser(user, store))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
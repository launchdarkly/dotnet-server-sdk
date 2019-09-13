using System.Collections.Generic;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    internal class Rule : VariationOrRollout
    {
        [JsonProperty(PropertyName = "id")]
        internal string Id { get; private set; }

        [JsonProperty(PropertyName = "clauses")]
        internal List<Clause> Clauses { get; private set; }

        [JsonConstructor]
        internal Rule(string id, int? variation, Rollout rollout, List<Clause> clauses) : base(variation, rollout)
        {
            Id = id;
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
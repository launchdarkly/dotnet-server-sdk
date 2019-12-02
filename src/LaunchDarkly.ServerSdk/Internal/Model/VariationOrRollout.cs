using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal class VariationOrRollout
    {
        [JsonProperty(PropertyName = "variation")]
        internal int? Variation { get; private set; }
        [JsonProperty(PropertyName = "rollout")]
        internal Rollout Rollout { get; private set; }

        [JsonConstructor]
        internal VariationOrRollout(int? variation, Rollout rollout)
        {
            Variation = variation;
            Rollout = rollout;
        }
    }
}
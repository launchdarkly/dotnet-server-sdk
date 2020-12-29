using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal class VariationOrRollout
    {
        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        internal int? Variation { get; private set; }
        [JsonProperty(PropertyName = "rollout", NullValueHandling = NullValueHandling.Ignore)]
        internal Rollout Rollout { get; private set; }

        [JsonConstructor]
        internal VariationOrRollout(int? variation, Rollout rollout)
        {
            Variation = variation;
            Rollout = rollout;
        }
    }
}
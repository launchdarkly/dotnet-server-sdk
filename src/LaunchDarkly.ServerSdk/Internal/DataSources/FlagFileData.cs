using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    // Represents the data structure that we parse files into, and provides the logic for
    // transferring its contents into the format used by the data store.
    internal sealed class FlagFileData
    {
        [JsonProperty(PropertyName = "flags", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> Flags { get; set; }

        [JsonProperty(PropertyName = "flagValues", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> FlagValues { get; set; }

        [JsonProperty(PropertyName = "segments", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, JToken> Segments { get; set; }
    }
}

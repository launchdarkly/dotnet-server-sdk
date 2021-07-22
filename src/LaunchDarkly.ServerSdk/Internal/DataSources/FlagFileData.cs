using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    // Represents the data structure that we parse files into, and provides the logic for
    // transferring its contents into the format used by the data store.
    internal sealed class FlagFileData
    {
        public Dictionary<string, FeatureFlag> Flags { get; set; }

        public Dictionary<string, LdValue> FlagValues { get; set; }

        public Dictionary<string, Segment> Segments { get; set; }
    }
}

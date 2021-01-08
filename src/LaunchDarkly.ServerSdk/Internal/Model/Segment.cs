﻿using System.Collections.Generic;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal sealed class Segment
    {
        [JsonProperty(PropertyName = "key")]
        public string Key { get; private set; }
        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }
        [JsonProperty(PropertyName = "included")]
        internal List<string> Included { get; private set; }
        [JsonProperty(PropertyName = "excluded")]
        internal List<string> Excluded { get; private set; }
        [JsonProperty(PropertyName = "salt")]
        internal string Salt { get; private set; }
        [JsonProperty(PropertyName = "rules")]
        internal List<SegmentRule> Rules { get; private set; }
        [JsonProperty(PropertyName = "deleted")]
        public bool Deleted { get; set; }

        [JsonConstructor]
        internal Segment(string key, int version, List<string> included, List<string> excluded,
                         string salt, List<SegmentRule> rules, bool deleted)
        {
            Key = key;
            Version = version;
            Included = included;
            Excluded = excluded;
            Salt = salt;
            Rules = rules ?? new List<SegmentRule>();
            Deleted = deleted;
        }

        internal Segment()
        {
        }
    }
}

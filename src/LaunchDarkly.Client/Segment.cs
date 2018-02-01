using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    public class Segment : IVersionedData
    {
        public string Key { get; private set; }
        public int Version { get; set; }
        internal List<string> Included { get; private set; }
        internal List<string> Excluded { get; private set; }
        internal string Salt { get; private set; }
        internal List<SegmentRule> Rules { get; private set; }
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
            Rules = rules;
            Deleted = deleted;
        }

        internal Segment()
        {
        }

        public bool MatchesUser(User user)
        {
            if (user.Key != null)
            {
                if (Included != null && Included.Contains(user.Key))
                {
                    return true;
                }
                if (Excluded != null && Excluded.Contains(user.Key))
                {
                    return false;
                }
                if (Rules != null)
                {
                    foreach (var rule in Rules)
                    {
                        if (rule.MatchesUser(user, this.Key, this.Salt))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}

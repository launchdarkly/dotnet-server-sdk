using System.Collections.Generic;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal class SegmentBuilder
    {
        private readonly string _key;
        private int _version;
        private List<string> _included = new List<string>();
        private List<string> _excluded = new List<string>();
        private List<SegmentRule> _rules = new List<SegmentRule>();
        private string _salt;
        private bool _deleted;

        internal SegmentBuilder(string key)
        {
            _key = key;
        }

        internal SegmentBuilder(Segment from)
        {
            _key = from.Key;
            _version = from.Version;
            _rules = from.Rules;
            _deleted = from.Deleted;
        }

        internal Segment Build()
        {
            return new Segment(_key, _version, _included, _excluded, _salt, _rules, _deleted);
        }

        internal SegmentBuilder Version(int version)
        {
            _version = version;
            return this;
        }

        internal SegmentBuilder Salt(string salt)
        {
            _salt = salt;
            return this;
        }

        internal SegmentBuilder Rules(List<SegmentRule> rules)
        {
            _rules = rules;
            return this;
        }

        internal SegmentBuilder Rules(params SegmentRule[] rules)
        {
            return Rules(new List<SegmentRule>(rules));
        }

        internal SegmentBuilder Deleted(bool deleted)
        {
            _deleted = deleted;
            return this;
        }
    }
}

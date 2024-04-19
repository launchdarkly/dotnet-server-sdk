﻿using System.Collections.Generic;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal class SegmentBuilder
    {
        private readonly string _key;
        private int _version;
        private ISet<string> _included = new HashSet<string>();
        private ISet<string> _excluded = new HashSet<string>();
        private List<SegmentTarget> _includedContexts = new List<SegmentTarget>();
        private List<SegmentTarget> _excludedContexts = new List<SegmentTarget>();
        private List<SegmentRule> _rules = new List<SegmentRule>();
        private string _salt;
        private bool _deleted;
        private bool _unbounded;
        private ContextKind? _unboundedContextKind;
        private int? _generation;

        internal SegmentBuilder(string key)
        {
            _key = key;
        }

        internal SegmentBuilder(Segment from)
        {
            _key = from.Key;
            _version = from.Version;
            _deleted = from.Deleted;
            _included = new HashSet<string>(from.Included);
            _excluded = new HashSet<string>(from.Excluded);
            _rules = new List<SegmentRule>(from.Rules);
            _salt = from.Salt;
            _unbounded = from.Unbounded;
            _unboundedContextKind = from.UnboundedContextKind;
            _generation = from.Generation;
        }

        internal Segment Build() =>
            new Segment(_key, _version, _deleted, _included, _excluded, _includedContexts, _excludedContexts,
                _rules, _salt, _unbounded, _unboundedContextKind, _generation);

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

        internal SegmentBuilder Included(params string[] keys)
        {
            foreach (var key in keys) { _included.Add(key); }
            return this;
        }

        internal SegmentBuilder Excluded(params string[] keys)
        {
            foreach (var key in keys) { _excluded.Add(key); }
            return this;
        }

        internal SegmentBuilder IncludedContext(ContextKind contextKind, params string[] keys)
        {
            _includedContexts.Add(new SegmentTarget(contextKind, new HashSet<string>(keys)));
            return this;
        }

        internal SegmentBuilder ExcludedContext(ContextKind contextKind, params string[] keys)
        {
            _excludedContexts.Add(new SegmentTarget(contextKind, new HashSet<string>(keys)));
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

        internal SegmentBuilder Unbounded(bool unbounded)
        {
            _unbounded = unbounded;
            return this;
        }

        internal SegmentBuilder UnboundedContextKind(ContextKind kind)
        {
            _unboundedContextKind = kind;
            return this;
        }

        internal SegmentBuilder Generation(int? generation)
        {
            _generation = generation;
            return this;
        }
    }

    internal class SegmentRuleBuilder
    {
        private readonly List<Clause> _clauses = new List<Clause>();
        private int? _weight;
        private ContextKind? _rolloutContextKind;
        private AttributeRef _bucketBy;

        internal SegmentRule Build() =>
            new SegmentRule(
                _clauses,
                _weight,
                _rolloutContextKind,
                _bucketBy
                );

        public SegmentRuleBuilder Clauses(params Clause[] clauses)
        {
            _clauses.AddRange(clauses);
            return this;
        }

        public SegmentRuleBuilder Weight(int? weight)
        {
            _weight = weight;
            return this;
        }

        public SegmentRuleBuilder RolloutContextKind(ContextKind rolloutContextKind)
        {
            _rolloutContextKind = rolloutContextKind;
            return this;
        }

        public SegmentRuleBuilder BucketBy(AttributeRef bucketBy)
        {
            _bucketBy = bucketBy;
            return this;
        }
    }
}

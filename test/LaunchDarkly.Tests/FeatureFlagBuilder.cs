using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Tests
{
    internal class FeatureFlagBuilder
    {
        private readonly string _key;
        private int _version;
        private bool _on;
        private List<Prerequisite> _prerequisites = new List<Prerequisite>();
        private string _salt;
        private List<Target> _targets = new List<Target>();
        private List<Rule> _rules = new List<Rule>();
        private VariationOrRollout _fallthrough;
        private int? _offVariation;
        private List<JToken> _variations;
        private bool _trackEvents;
        private long? _debugEventsUntilDate;
        private bool _deleted;
        private bool _clientSide;

        internal FeatureFlagBuilder(string key)
        {
            _key = key;
        }

        internal FeatureFlagBuilder(FeatureFlag from)
        {
            _key = from.Key;
            _version = from.Version;
            _on = from.On;
            _prerequisites = from.Prerequisites;
            _salt = from.Salt;
            _targets = from.Targets;
            _rules = from.Rules;
            _fallthrough = from.Fallthrough;
            _offVariation = from.OffVariation;
            _variations = from.Variations;
            _trackEvents = from.TrackEvents;
            _debugEventsUntilDate = from.DebugEventsUntilDate;
            _deleted = from.Deleted;
            _clientSide = from.ClientSide;
        }

        internal FeatureFlag Build()
        {
            return new FeatureFlag(_key, _version, _on, _prerequisites, _salt,
                _targets, _rules, _fallthrough, _offVariation, _variations,
                _trackEvents, _debugEventsUntilDate, _deleted, _clientSide);
        }

        internal FeatureFlagBuilder Version(int version)
        {
            _version = version;
            return this;
        }

        internal FeatureFlagBuilder On(bool on)
        {
            _on = on;
            return this;
        }

        internal FeatureFlagBuilder Prerequisites(List<Prerequisite> prerequisites)
        {
            _prerequisites = prerequisites;
            return this;
        }

        internal FeatureFlagBuilder Salt(string salt)
        {
            _salt = salt;
            return this;
        }

        internal FeatureFlagBuilder Targets(List<Target> targets)
        {
            _targets = targets;
            return this;
        }

        internal FeatureFlagBuilder Rules(List<Rule> rules)
        {
            _rules = rules;
            return this;
        }

        internal FeatureFlagBuilder Fallthrough(VariationOrRollout fallthrough)
        {
            _fallthrough = fallthrough;
            return this;
        }

        internal FeatureFlagBuilder OffVariation(int? offVariation)
        {
            _offVariation = offVariation;
            return this;
        }

        internal FeatureFlagBuilder Variations(List<JToken> variations)
        {
            _variations = variations;
            return this;
        }

        internal FeatureFlagBuilder TrackEvents(bool trackEvents)
        {
            _trackEvents = trackEvents;
            return this;
        }

        internal FeatureFlagBuilder DebugEventsUntilDate(long? debugEventsUntilDate)
        {
            _debugEventsUntilDate = debugEventsUntilDate;
            return this;
        }

        internal FeatureFlagBuilder ClientSide(bool clientSide)
        {
            _clientSide = clientSide;
            return this;
        }

        internal FeatureFlagBuilder Deleted(bool deleted)
        {
            _deleted = deleted;
            return this;
        }

        internal FeatureFlagBuilder OffWithValue(JToken value)
        {
            _on = false;
            _offVariation = 0;
            _variations = new List<JToken> { value };
            return this;
        }

        internal FeatureFlagBuilder BooleanWithClauses(params Clause[] clauses)
        {
            _on = true;
            _offVariation = 0;
            _variations = new List<JToken> { new JValue(false), new JValue(true) };
            _rules = new List<Rule> { new Rule(1, null, new List<Clause>(clauses)) };
            return this;
        }
    }
}

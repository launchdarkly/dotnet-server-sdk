using System.Collections.Generic;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal class FeatureFlagBuilder
    {
        private readonly string _key;
        private int _version;
        private bool _on;
        private List<Prerequisite> _prerequisites = new List<Prerequisite>();
        private string _salt;
        private List<Target> _targets = new List<Target>();
        private List<FlagRule> _rules = new List<FlagRule>();
        private VariationOrRollout _fallthrough;
        private int? _offVariation;
        private List<LdValue> _variations;
        private bool _trackEvents;
        private bool _trackEventsFallthrough;
        private UnixMillisecondTime? _debugEventsUntilDate;
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
            _prerequisites = new List<Prerequisite>(from.Prerequisites);
            _salt = from.Salt;
            _targets = new List<Target>(from.Targets);
            _rules = new List<FlagRule>(from.Rules);
            _fallthrough = from.Fallthrough;
            _offVariation = from.OffVariation;
            _variations = new List<LdValue>(from.Variations);
            _trackEvents = from.TrackEvents;
            _trackEventsFallthrough = from.TrackEventsFallthrough;
            _debugEventsUntilDate = from.DebugEventsUntilDate;
            _deleted = from.Deleted;
            _clientSide = from.ClientSide;
        }

        internal FeatureFlag Build()
        {
            return new FeatureFlag(_key, _version, _deleted, _on, _prerequisites,
                _targets, _rules, _fallthrough, _offVariation, _variations, _salt,
                _trackEvents, _trackEventsFallthrough, _debugEventsUntilDate, _clientSide);
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

        internal FeatureFlagBuilder Prerequisites(params Prerequisite[] prerequisites)
        {
            return Prerequisites(new List<Prerequisite>(prerequisites));
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

        internal FeatureFlagBuilder Targets(params Target[] targets)
        {
            return Targets(new List<Target>(targets));
        }

        internal FeatureFlagBuilder Rules(List<FlagRule> rules)
        {
            _rules = rules;
            return this;
        }

        internal FeatureFlagBuilder Rules(params FlagRule[] rules)
        {
            return Rules(new List<FlagRule>(rules));
        }

        internal FeatureFlagBuilder Fallthrough(VariationOrRollout fallthrough)
        {
            _fallthrough = fallthrough;
            return this;
        }

        internal FeatureFlagBuilder FallthroughVariation(int variation)
        {
            _fallthrough = new VariationOrRollout(variation, null);
            return this;
        }

        internal FeatureFlagBuilder FallthroughRollout(Rollout rollout)
        {
            return Fallthrough(new VariationOrRollout(null, rollout));
        }

        internal FeatureFlagBuilder OffVariation(int? offVariation)
        {
            _offVariation = offVariation;
            return this;
        }

        internal FeatureFlagBuilder Variations(List<LdValue> variations)
        {
            _variations = variations;
            return this;
        }

        internal FeatureFlagBuilder Variations(params LdValue[] variations)
        {
            return Variations(new List<LdValue>(variations));
        }

        internal FeatureFlagBuilder GeneratedVariations(int count)
        {
            var list = new List<LdValue>();
            for (var i = 0; i < count; i++)
            {
                list.Add(LdValue.Of(i));
            }
            return Variations(list);
        }

        internal FeatureFlagBuilder TrackEvents(bool trackEvents)
        {
            _trackEvents = trackEvents;
            return this;
        }

        internal FeatureFlagBuilder TrackEventsFallthrough(bool trackEventsFallthrough)
        {
            _trackEventsFallthrough = trackEventsFallthrough;
            return this;
        }

        internal FeatureFlagBuilder DebugEventsUntilDate(UnixMillisecondTime? debugEventsUntilDate)
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

        internal FeatureFlagBuilder OffWithValue(LdValue value)
        {
            return On(false).OffVariation(0).Variations(value);
        }

        internal FeatureFlagBuilder BooleanWithClauses(params Clause[] clauses)
        {
            return On(true).OffVariation(0)
                .FallthroughVariation(0)
                .Variations(LdValue.Of(false), LdValue.Of(true))
                .Rules(new RuleBuilder().Id("id").Variation(1).Clauses(clauses).Build());
        }

        internal FeatureFlagBuilder BooleanMatchingSegment(string segmentKey)
        {
            return BooleanWithClauses(ClauseBuilder.ShouldMatchSegment(segmentKey));
        }
    }

    internal class RuleBuilder
    {
        private string _id = "";
        private int? _variation = null;
        private Rollout? _rollout = null;
        private List<Clause> _clauses = new List<Clause>();
        private bool _trackEvents = false;

        internal FlagRule Build()
        {
            return new FlagRule(_variation, _rollout, _id, _clauses, _trackEvents);
        }

        internal RuleBuilder Id(string id)
        {
            _id = id;
            return this;
        }

        internal RuleBuilder Variation(int? variation)
        {
            _variation = variation;
            return this;
        }

        internal RuleBuilder Rollout(Rollout rollout)
        {
            _rollout = rollout;
            return this;
        }

        internal RuleBuilder Clauses(List<Clause> clauses)
        {
            _clauses = clauses;
            return this;
        }

        internal RuleBuilder Clauses(params Clause[] clauses)
        {
            return Clauses(new List<Clause>(clauses));
        }

        internal RuleBuilder TrackEvents(bool trackEvents)
        {
            _trackEvents = trackEvents;
            return this;
        }
    }

    internal class ClauseBuilder
    {
        private UserAttribute _attribute;
        private Operator _op;
        private List<LdValue> _values = new List<LdValue>();
        private bool _negate;

        internal Clause Build()
        {
            return new Clause(_attribute, _op, _values, _negate);
        }

        public ClauseBuilder Attribute(string attribute) =>
            Attribute(UserAttribute.ForName(attribute));

        public ClauseBuilder Attribute(UserAttribute attribute)
        {
            _attribute = attribute;
            return this;
        }

        public ClauseBuilder Op(Operator op)
        {
            _op = op;
            return this;
        }

        public ClauseBuilder Op(string opName) => Op(Operator.ForName(opName));

        public ClauseBuilder Values(List<LdValue> values)
        {
            _values = values;
            return this;
        }

        public ClauseBuilder Values(params LdValue[] values)
        {
            return Values(new List<LdValue>(values));
        }

        public ClauseBuilder Negate(bool negate)
        {
            _negate = negate;
            return this;
        }

        public ClauseBuilder KeyIs(string key)
        {
            return Attribute("key").Op("in").Values(LdValue.Of(key));
        }

        public static Clause ShouldMatchUser(User user)
        {
            return new ClauseBuilder().KeyIs(user.Key).Build();
        }

        public static Clause ShouldNotMatchUser(User user)
        {
            return new ClauseBuilder().KeyIs(user.Key).Negate(true).Build();
        }

        public static Clause ShouldMatchSegment(string segmentKey)
        {
            return new ClauseBuilder().Attribute("").Op("segmentMatch").Values(LdValue.Of(segmentKey)).Build();
        }
    }
}

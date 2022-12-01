using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
        private List<Target> _contextTargets = new List<Target>();
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
            _contextTargets = new List<Target>(from.ContextTargets);
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
                _targets.ToImmutableList(), _contextTargets.ToImmutableList(), _rules,
                _fallthrough, _offVariation, _variations, _salt,
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

        internal FeatureFlagBuilder Targets(IEnumerable<Target> targets)
        {
            _targets = new List<Target>(targets);
            return this;
        }

        internal FeatureFlagBuilder Targets(params Target[] targets) =>
            Targets((IEnumerable<Target>)targets);

        internal FeatureFlagBuilder ContextTargets(IEnumerable<Target> targets)
        {
            _contextTargets = new List<Target>(targets);
            return this;
        }

        internal FeatureFlagBuilder ContextTargets(params Target[] targets) =>
            ContextTargets((IEnumerable<Target>)(targets));

        internal FeatureFlagBuilder Rules(IEnumerable<FlagRule> rules)
        {
            _rules = new List<FlagRule>(rules);
            return this;
        }

        internal FeatureFlagBuilder Rules(params FlagRule[] rules) =>
            Rules((IEnumerable<FlagRule>)(rules));

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

        internal FeatureFlagBuilder Variations(IEnumerable<LdValue> variations)
        {
            _variations = new List<LdValue>(variations);
            return this;
        }

        internal FeatureFlagBuilder Variations(params LdValue[] variations) =>
            Variations((IEnumerable<LdValue>)(variations));

        internal FeatureFlagBuilder Variations(params string[] variations) =>
            Variations(variations.Select(v => LdValue.Of(v)));

        internal FeatureFlagBuilder Variations(params bool[] variations) =>
            Variations(variations.Select(v => LdValue.Of(v)));

        internal FeatureFlagBuilder Variations(params int[] variations) =>
            Variations(variations.Select(v => LdValue.Of(v)));

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
                .Variations(false, true)
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
        private ContextKind? _contextKind;
        private AttributeRef _attribute;
        private Operator _op;
        private List<LdValue> _values = new List<LdValue>();
        private bool _negate;

        internal Clause Build()
        {
            return new Clause(_contextKind, _attribute, _op, _values, _negate);
        }

        public ClauseBuilder ContextKind(ContextKind contextKind)
        {
            _contextKind = contextKind;
            return this;
        }

        public ClauseBuilder ContextKind(string contextKind) => ContextKind(Sdk.ContextKind.Of(contextKind));

        public ClauseBuilder Attribute(string attribute) =>
            Attribute(AttributeRef.FromPath(attribute));

        public ClauseBuilder Attribute(AttributeRef attribute)
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

        public ClauseBuilder Values(IEnumerable<LdValue> values)
        {
            _values = new List<LdValue>(values);
            return this;
        }

        public ClauseBuilder Values(params LdValue[] values) =>
            Values(values.ToImmutableList());

        public ClauseBuilder Values(params string[] values) =>
            Values(values.Select(v => LdValue.Of(v)));

        public ClauseBuilder Values(params bool[] values) =>
            Values(values.Select(v => LdValue.Of(v)));

        public ClauseBuilder Values(params int[] values) =>
            Values(values.Select(v => LdValue.Of(v)));

        public ClauseBuilder Negate(bool negate)
        {
            _negate = negate;
            return this;
        }

        public ClauseBuilder KeyIs(string key) =>
            Attribute("key").Op("in").Values(key);

        public static Clause ShouldMatchUser(Context user) =>
            new ClauseBuilder().KeyIs(user.Key).Build();

        public static Clause ShouldMatchAnyUser() =>
            new ClauseBuilder().Attribute("key").Op("in").Values("").Negate(true).Build();

        public static Clause ShouldMatchAnyContext() =>
            new ClauseBuilder().Attribute("kind").Op("in").Values("").Negate(true).Build();

        public static Clause ShouldNotMatchUser(Context user) =>
            new ClauseBuilder().KeyIs(user.Key).Negate(true).Build();

        public static Clause ShouldMatchSegment(string segmentKey) =>
            new ClauseBuilder().Attribute("").Op("segmentMatch").Values(segmentKey).Build();
    }

    internal class TargetBuilder
    {
        public static Target ContextTarget(ContextKind? contextKind, int variation, params string[] values) =>
            new Target(contextKind, ImmutableList.CreateRange(values), variation);

        public static Target UserTarget(int variation, params string[] values) =>
            ContextTarget(null, variation, values);
    }
}

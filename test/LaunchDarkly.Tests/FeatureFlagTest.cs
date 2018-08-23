using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using LaunchDarkly.Common;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class FeatureFlagTest
    {
        private static readonly User baseUser = User.WithKey("userkey");

        private readonly IFeatureStore featureStore = new InMemoryFeatureStore();

        [Fact]
        public void FlagReturnsOffVariationIfFlagIsOff()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("off"), 1, EvaluationReason.Off.Instance);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsNullIfFlagIsOffAndOffVariationIsUnspecified()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null, EvaluationReason.Off.Instance);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsErrorIfFlagIsOffAndOffVariationIsTooHigh()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .OffVariation(999)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsErrorIfFlagIsOffAndOffVariationIsNegative()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .OffVariation(-1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsFallthroughIfFlagIsOnAndThereAreNoRules()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("fall"), 0, EvaluationReason.Fallthrough.Instance);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsErrorIfFallthroughHasTooHighVariation()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .OffVariation(1)
                .FallthroughVariation(999)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsErrorIfFallthroughHasNegativeVariation()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .OffVariation(1)
                .FallthroughVariation(-1)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsErrorIfFallthroughHasNeitherVariationNorRollout()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .OffVariation(1)
                .Fallthrough(new VariationOrRollout(null, null))
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsErrorIfFallthroughHasEmptyRolloutVariationList()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .OffVariation(1)
                .Fallthrough(new VariationOrRollout(null, new Rollout(new List<WeightedVariation>(), null)))
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsOffVariationIfPrerequisiteIsNotFound()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature1", 1) })
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("off"), 1,
                new EvaluationReason.PrerequisitesFailed(new List<string> { "feature1" }));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsOffVariationAndEventIfPrerequisiteIsNotMet()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature1", 1) })
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("nogo"), new JValue("go") })
                .Version(2)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("off"), 1,
                new EvaluationReason.PrerequisitesFailed(new List<string> { "feature1" }));
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(new JValue("nogo"), e.Value);
            Assert.Equal(f1.Version, e.Version);
            Assert.Equal(f0.Key, e.PrereqOf);
        }

        [Fact]
        public void FlagReturnsFallthroughVariationAndEventIfPrerequisiteIsMetAndThereAreNoRules()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature1", 1) })
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(1) // this is what makes the prerequisite pass
                .Variations(new List<JToken> { new JValue("nogo"), new JValue("go") })
                .Version(2)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("fall"), 0, EvaluationReason.Fallthrough.Instance);
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(new JValue("go"), e.Value);
            Assert.Equal(f1.Version, e.Version);
            Assert.Equal(f0.Key, e.PrereqOf);
        }

        [Fact]
        public void MultipleLevelsOfPrerequisitesProduceMultipleEvents()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature1", 1) })
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature2", 1) })
                .FallthroughVariation(1)
                .Variations(new List<JToken> { new JValue("nogo"), new JValue("go") })
                .Version(2)
                .Build();
            var f2 = new FeatureFlagBuilder("feature2")
                .On(true)
                .FallthroughVariation(1)
                .Variations(new List<JToken> { new JValue("nogo"), new JValue("go") })
                .Version(3)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);
            featureStore.Upsert(VersionedDataKind.Features, f2);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("fall"), 0, EvaluationReason.Fallthrough.Instance);
            Assert.Equal(expected, result.Result);

            Assert.Equal(2, result.PrerequisiteEvents.Count);

            FeatureRequestEvent e0 = result.PrerequisiteEvents[0];
            Assert.Equal(f2.Key, e0.Key);
            Assert.Equal(new JValue("go"), e0.Value);
            Assert.Equal(f2.Version, e0.Version);
            Assert.Equal(f1.Key, e0.PrereqOf);

            FeatureRequestEvent e1 = result.PrerequisiteEvents[1];
            Assert.Equal(f1.Key, e1.Key);
            Assert.Equal(new JValue("go"), e1.Value);
            Assert.Equal(f1.Version, e1.Version);
            Assert.Equal(f0.Key, e1.PrereqOf);
        }

        [Fact]
        public void MultiplePrerequisiteFailuresAreAllRecorded()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new List<Prerequisite> { new Prerequisite("feature1", 0),
                    new Prerequisite("feature2", 0) })
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(1)
                .Variations(new List<JToken> { new JValue("nogo"), new JValue("go") })
                .Version(2)
                .Build();
            var f2 = new FeatureFlagBuilder("feature2")
                .On(true)
                .FallthroughVariation(1)
                .Variations(new List<JToken> { new JValue("nogo"), new JValue("go") })
                .Version(3)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);
            featureStore.Upsert(VersionedDataKind.Features, f2);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("off"), 1,
                new EvaluationReason.PrerequisitesFailed(new List<string> { "feature1", "feature2" }));
            Assert.Equal(expected, result.Result);

            Assert.Equal(2, result.PrerequisiteEvents.Count);

            FeatureRequestEvent e0 = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e0.Key);

            FeatureRequestEvent e1 = result.PrerequisiteEvents[1];
            Assert.Equal(f2.Key, e1.Key);
        }

        [Fact]
        public void FlagMatchesUserFromTargets()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .Targets(new List<Target> { new Target(new List<string> { "whoever", "userkey" }, 2) })
                .FallthroughVariation(0)
                .OffVariation(1)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
            var user = User.WithKey("userkey");
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("on"), 2, EvaluationReason.TargetMatch.Instance);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagMatchesUserFromRules()
        {
            var clause0 = new Clause("key", "in", new List<JValue> { new JValue("wrongkey") }, false);
            var clause1 = new Clause("key", "in", new List<JValue> { new JValue("userkey") }, false);
            var rule0 = new Rule("ruleid0", 2, null, new List<Clause> { clause0 });
            var rule1 = new Rule("ruleid1", 2, null, new List<Clause> { clause1 });
            var f = FeatureFlagWithRules(rule0, rule1);

            var user = User.WithKey("userkey");
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(new JValue("on"), 2,
                new EvaluationReason.RuleMatch(1, "ruleid1"));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithTooHighVariationReturnsMalformedFlagError()
        {
            var clause = new Clause("key", "in", new List<JValue> { new JValue("userkey") }, false);
            var rule = new Rule("ruleid", 999, null, new List<Clause> { clause });
            var f = FeatureFlagWithRules(rule);
            
            var user = User.WithKey("userkey");
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithNegativeVariationReturnsMalformedFlagError()
        {
            var clause = new Clause("key", "in", new List<JValue> { new JValue("userkey") }, false);
            var rule = new Rule("ruleid", -1, null, new List<Clause> { clause });
            var f = FeatureFlagWithRules(rule);
            
            var user = User.WithKey("userkey");
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithNoVariationOrRolloutReturnsMalformedFlagError()
        {
            var clause = new Clause("key", "in", new List<JValue> { new JValue("userkey") }, false);
            var rule = new Rule("ruleid", null, null, new List<Clause> { clause });
            var f = FeatureFlagWithRules(rule);

            var user = User.WithKey("userkey");
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithRolloutWithEmptyVariationsListReturnsMalformedFlagError()
        {
            var clause = new Clause("key", "in", new List<JValue> { new JValue("userkey") }, false);
            var rule = new Rule("ruleid", null,
                new Rollout(new List<WeightedVariation>(), null),
                new List<Clause> { clause });
            var f = FeatureFlagWithRules(rule);

            var user = User.WithKey("userkey");
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<JToken>(null, null,
                new EvaluationReason.Error(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void ClauseCanMatchBuiltInAttribute()
        {
            var clause = new Clause("name", "in", new List<JValue> { new JValue("Bob") }, false);
            var f = BooleanFlagWithClauses(clause);
            var user = User.WithKey("key").AndName("Bob");

            Assert.Equal(new JValue(true), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseCanMatchCustomAttribute()
        {
            var clause = new Clause("legs", "in", new List<JValue> { new JValue(4) }, false);
            var f = BooleanFlagWithClauses(clause);
            var user = User.WithKey("key").AndCustomAttribute("legs", 4);

            Assert.Equal(new JValue(true), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseReturnsFalseForMissingAttribute()
        {
            var clause = new Clause("legs", "in", new List<JValue> { new JValue(4) }, false);
            var f = BooleanFlagWithClauses(clause);
            var user = User.WithKey("key").AndName("bob");

            Assert.Equal(new JValue(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseCanBeNegated()
        {
            var clause = new Clause("name", "in", new List<JValue> { new JValue("Bob") }, true);
            var f = BooleanFlagWithClauses(clause);
            var user = User.WithKey("key").AndName("Bob");

            Assert.Equal(new JValue(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseWithUnknownOperatorDoesNotMatch()
        {
            var clause = new Clause("name", "invalidOp", new List<JValue> { new JValue("Bob") }, false);
            var f = BooleanFlagWithClauses(clause);
            var user = User.WithKey("key").AndName("Bob");

            Assert.Equal(new JValue(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }
        
        [Fact]
        public void SegmentMatchClauseRetrievesSegmentFromStore()
        {
            var segment = new Segment("segkey", 1, new List<string> { "foo" }, new List<string>(), "",
                new List<SegmentRule>(), false);
            featureStore.Upsert(VersionedDataKind.Segments, segment);

            var f = SegmentMatchBooleanFlag("segkey");
            var user = User.WithKey("foo");

            Assert.Equal(new JValue(true), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void SegmentMatchClauseFallsThroughIfSegmentNotFound()
        {
            var f = SegmentMatchBooleanFlag("segkey");
            var user = User.WithKey("foo");

            Assert.Equal(new JValue(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        private FeatureFlag FeatureFlagWithRules(params Rule[] rules)
        {
            return new FeatureFlagBuilder("feature")
                .On(true)
                .Rules(new List<Rule>(rules))
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue("fall"), new JValue("off"), new JValue("on") })
                .Build();
        }

        private FeatureFlag BooleanFlagWithClauses(params Clause[] clauses)
        {
            var rule = new Rule("id", 1, null, new List<Clause>(clauses));
            return new FeatureFlagBuilder("feature")
                .On(true)
                .Rules(new List<Rule> { rule })
                .FallthroughVariation(0)
                .Variations(new List<JToken> { new JValue(false), new JValue(true) })
                .Build();
        }

        private FeatureFlag SegmentMatchBooleanFlag(string segmentKey)
        {
            var clause = new Clause("", "segmentMatch", new List<JValue> { new JValue(segmentKey) }, false);
            return BooleanFlagWithClauses(clause);
        }
    }
}

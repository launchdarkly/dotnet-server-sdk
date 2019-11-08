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

        private readonly IFeatureStore featureStore = TestUtils.InMemoryFeatureStore();

        [Fact]
        public void FlagReturnsOffVariationIfFlagIsOff()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("off"), 1, EvaluationReason.OffReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsNullIfFlagIsOffAndOffVariationIsUnspecified()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.OffReason);
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
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
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
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
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
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("fall"), 0, EvaluationReason.FallthroughReason);
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
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
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
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
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
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
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
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsOffVariationIfPrerequisiteIsNotFound()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("off"), 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsOffVariationAndEventIfPrerequisiteIsOff()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(false)
                .OffVariation(1)
                // note that even though it returns the desired variation, it is still off and therefore not a match
                .Variations(new JValue("nogo"), new JValue("go"))
                .Version(2)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("off"), 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(LdValue.Of("go"), e.LdValue);
            Assert.Equal(f1.Version, e.Version);
            Assert.Equal(f0.Key, e.PrereqOf);
        }

        [Fact]
        public void FlagReturnsOffVariationAndEventIfPrerequisiteIsNotMet()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(0)
                .Variations(new JValue("nogo"), new JValue("go"))
                .Version(2)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("off"), 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(LdValue.Of("nogo"), e.LdValue);
            Assert.Equal(f1.Version, e.Version);
            Assert.Equal(f0.Key, e.PrereqOf);
        }

        [Fact]
        public void FlagReturnsFallthroughVariationAndEventIfPrerequisiteIsMetAndThereAreNoRules()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(1) // this is what makes the prerequisite pass
                .Variations(new JValue("nogo"), new JValue("go"))
                .Version(2)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("fall"), 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(LdValue.Of("go"), e.LdValue);
            Assert.Equal(f1.Version, e.Version);
            Assert.Equal(f0.Key, e.PrereqOf);
        }

        [Fact]
        public void MultipleLevelsOfPrerequisitesProduceMultipleEvents()
        {
            var f0 = new FeatureFlagBuilder("feature0")
                .On(true)
                .Prerequisites(new Prerequisite("feature1", 1))
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .Prerequisites(new Prerequisite("feature2", 1))
                .FallthroughVariation(1)
                .Variations(new JValue("nogo"), new JValue("go"))
                .Version(2)
                .Build();
            var f2 = new FeatureFlagBuilder("feature2")
                .On(true)
                .FallthroughVariation(1)
                .Variations(new JValue("nogo"), new JValue("go"))
                .Version(3)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, f1);
            featureStore.Upsert(VersionedDataKind.Features, f2);

            var result = f0.Evaluate(baseUser, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("fall"), 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Equal(2, result.PrerequisiteEvents.Count);

            FeatureRequestEvent e0 = result.PrerequisiteEvents[0];
            Assert.Equal(f2.Key, e0.Key);
            Assert.Equal(LdValue.Of("go"), e0.LdValue);
            Assert.Equal(f2.Version, e0.Version);
            Assert.Equal(f1.Key, e0.PrereqOf);

            FeatureRequestEvent e1 = result.PrerequisiteEvents[1];
            Assert.Equal(f1.Key, e1.Key);
            Assert.Equal(LdValue.Of("go"), e1.LdValue);
            Assert.Equal(f1.Version, e1.Version);
            Assert.Equal(f0.Key, e1.PrereqOf);
        }
        
        [Fact]
        public void FlagMatchesUserFromTargets()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .Targets(new Target(new List<string> { "whoever", "userkey" }, 2))
                .FallthroughVariation(0)
                .OffVariation(1)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
            var user = User.WithKey("userkey");
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("on"), 2, EvaluationReason.TargetMatchReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagMatchesUserFromRules()
        {
            var user = User.WithKey("userkey");
            var clause0 = ClauseBuilder.ShouldNotMatchUser(user);
            var clause1 = ClauseBuilder.ShouldMatchUser(user);
            var rule0 = new RuleBuilder().Id("ruleid0").Variation(2).Clauses(clause0).Build();
            var rule1 = new RuleBuilder().Id("ruleid1").Variation(2).Clauses(clause1).Build();
            var f = FeatureFlagWithRules(rule0, rule1);

            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Of("on"), 2,
                EvaluationReason.RuleMatchReason(1, "ruleid1"));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithTooHighVariationReturnsMalformedFlagError()
        {
            var user = User.WithKey("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Variation(999).Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);
            
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithNegativeVariationReturnsMalformedFlagError()
        {
            var user = User.WithKey("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Variation(-1).Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);
            
            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithNoVariationOrRolloutReturnsMalformedFlagError()
        {
            var user = User.WithKey("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);

            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithRolloutWithEmptyVariationsListReturnsMalformedFlagError()
        {
            var user = User.WithKey("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Clauses(clause)
                .Rollout(new Rollout(new List<WeightedVariation>(), null)).Build();
            var f = FeatureFlagWithRules(rule);

            var result = f.Evaluate(user, featureStore, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void ClauseCanMatchBuiltInAttribute()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("in").Values(new JValue("Bob")).Build();
            var f = BooleanFlagWithClauses(clause);
            var user = User.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(true), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseCanMatchCustomAttribute()
        {
            var clause = new ClauseBuilder().Attribute("legs").Op("in").Values(new JValue(4)).Build();
            var f = BooleanFlagWithClauses(clause);
            var user = User.Builder("key").Custom("legs", 4).Build();

            Assert.Equal(LdValue.Of(true), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseReturnsFalseForMissingAttribute()
        {
            var clause = new ClauseBuilder().Attribute("legs").Op("in").Values(new JValue(4)).Build();
            var f = BooleanFlagWithClauses(clause);
            var user = User.Builder("key").Name("bob").Build();

            Assert.Equal(LdValue.Of(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseCanBeNegated()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("in").Values(new JValue("Bob"))
                .Negate(true).Build();
            var f = BooleanFlagWithClauses(clause);
            var user = User.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseWithUnknownOperatorDoesNotMatch()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("invalidOp").Values(new JValue("Bob")).Build();
            var f = BooleanFlagWithClauses(clause);
            var user = User.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }
        
        [Fact]
        public void SegmentMatchClauseRetrievesSegmentFromStore()
        {
            var segment = new Segment("segkey", 1, new List<string> { "foo" }, new List<string>(), "",
                new List<SegmentRule>(), false);
            featureStore.Upsert(VersionedDataKind.Segments, segment);

            var f = SegmentMatchBooleanFlag("segkey");
            var user = User.WithKey("foo");

            Assert.Equal(LdValue.Of(true), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void SegmentMatchClauseFallsThroughIfSegmentNotFound()
        {
            var f = SegmentMatchBooleanFlag("segkey");
            var user = User.WithKey("foo");

            Assert.Equal(LdValue.Of(false), f.Evaluate(user, featureStore, EventFactory.Default).Result.Value);
        }

        private FeatureFlag FeatureFlagWithRules(params Rule[] rules)
        {
            return new FeatureFlagBuilder("feature")
                .On(true)
                .Rules(rules)
                .FallthroughVariation(0)
                .Variations(new JValue("fall"), new JValue("off"), new JValue("on"))
                .Build();
        }

        private FeatureFlag BooleanFlagWithClauses(params Clause[] clauses)
        {
            var rule = new RuleBuilder().Id("id").Variation(1).Clauses(clauses).Build();
            return new FeatureFlagBuilder("feature")
                .On(true)
                .Rules(rule)
                .FallthroughVariation(0)
                .Variations(new JValue(false), new JValue(true))
                .Build();
        }

        private FeatureFlag SegmentMatchBooleanFlag(string segmentKey)
        {
            var clause = new ClauseBuilder().Op("segmentMatch").Values(new JValue(segmentKey)).Build();
            return BooleanFlagWithClauses(clause);
        }
    }
}

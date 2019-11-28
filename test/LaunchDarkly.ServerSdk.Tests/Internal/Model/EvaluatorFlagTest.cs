using System.Collections.Generic;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Internal.Events;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Model.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    // Tests of flag evaluation at the highest level. Rule-level and clause-level behavior is covered
    // in detail in EvaluatorRuleTest, EvaluatorClauseTest, and EvaluatorSegmentMatchTest.

    public class EvaluatorFlagTest
    {
        private static readonly User baseUser = User.WithKey("userkey");
        private static readonly LdValue fallthroughValue = LdValue.Of("fallthrough");
        private static readonly LdValue offValue = LdValue.Of("off");
        private static readonly LdValue onValue = LdValue.Of("on");
        
        [Fact]
        public void FlagReturnsOffVariationIfFlagIsOff()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .OffVariation(1)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(offValue, 1, EvaluationReason.OffReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void FlagReturnsNullIfFlagIsOffAndOffVariationIsUnspecified()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var evaluator = BasicEvaluator.WithNonexistentFlag("feature1");
            var result = evaluator.Evaluate(f0, baseUser, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
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
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(false)
                .OffVariation(1)
                // note that even though it returns the desired variation, it is still off and therefore not a match
                .Variations(LdValue.Of("nogo"), LdValue.Of("go"))
                .Version(2)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1);

            var result = evaluator.Evaluate(f0, baseUser, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(LdValue.Of("go"), e.Value);
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
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(0)
                .Variations(LdValue.Of("nogo"), LdValue.Of("go"))
                .Version(2)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1);

            var result = evaluator.Evaluate(f0, baseUser, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(LdValue.Of("nogo"), e.Value);
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
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .FallthroughVariation(1) // this is what makes the prerequisite pass
                .Variations(LdValue.Of("nogo"), LdValue.Of("go"))
                .Version(2)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1);

            var result = evaluator.Evaluate(f0, baseUser, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Equal(1, result.PrerequisiteEvents.Count);
            FeatureRequestEvent e = result.PrerequisiteEvents[0];
            Assert.Equal(f1.Key, e.Key);
            Assert.Equal(LdValue.Of("go"), e.Value);
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
                .Variations(fallthroughValue, offValue, onValue)
                .Version(1)
                .Build();
            var f1 = new FeatureFlagBuilder("feature1")
                .On(true)
                .Prerequisites(new Prerequisite("feature2", 1))
                .FallthroughVariation(1)
                .Variations(LdValue.Of("nogo"), LdValue.Of("go"))
                .Version(2)
                .Build();
            var f2 = new FeatureFlagBuilder("feature2")
                .On(true)
                .FallthroughVariation(1)
                .Variations(LdValue.Of("nogo"), LdValue.Of("go"))
                .Version(3)
                .Build();
            var evaluator = BasicEvaluator.WithStoredFlags(f1, f2);

            var result = evaluator.Evaluate(f0, baseUser, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Equal(2, result.PrerequisiteEvents.Count);

            FeatureRequestEvent e0 = result.PrerequisiteEvents[0];
            Assert.Equal(f2.Key, e0.Key);
            Assert.Equal(LdValue.Of("go"), e0.Value);
            Assert.Equal(f2.Version, e0.Version);
            Assert.Equal(f1.Key, e0.PrereqOf);

            FeatureRequestEvent e1 = result.PrerequisiteEvents[1];
            Assert.Equal(f1.Key, e1.Key);
            Assert.Equal(LdValue.Of("go"), e1.Value);
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
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var user = User.WithKey("userkey");
            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(onValue, 2, EvaluationReason.TargetMatchReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        private FeatureFlag FeatureFlagWithRules(params Rule[] rules)
        {
            return new FeatureFlagBuilder("feature")
                .On(true)
                .Rules(rules)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
        }
    }
}

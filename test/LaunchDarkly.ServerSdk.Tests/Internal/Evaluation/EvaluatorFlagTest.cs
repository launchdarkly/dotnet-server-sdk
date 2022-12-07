using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation at the highest level. More specific areas are covered in detail in
    // EvaluatorTargetTest, EvaluatorRuleTest, EvaluatorClauseTest, and EvaluatorSegmentMatchTest.

    public class EvaluatorFlagTest
    {
        private static readonly Context baseUser = Context.New("userkey");
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
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(offValue, 1, EvaluationReason.OffReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
        }

        [Fact]
        public void FlagReturnsNullIfFlagIsOffAndOffVariationIsUnspecified()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(false)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.OffReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
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
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
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
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
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
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
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
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
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
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
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
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
        }

        [Fact]
        public void FlagReturnsErrorIfFallthroughHasEmptyRolloutVariationList()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .OffVariation(1)
                .FallthroughRollout(new Rollout(RolloutKind.Rollout, null, null, new List<WeightedVariation>(), new AttributeRef()))
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
        }

        [Fact]
        public void FlagReturnsInExperimentForFallthroughWhenInExperimentVariation()
        {
            var rollout = BuildRollout(RolloutKind.Experiment, false);
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .FallthroughRollout(rollout)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser);

            Assert.Equal(EvaluationReasonKind.Fallthrough, result.Result.Reason.Kind);
            Assert.True(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void FlagReturnsNotInExperimentForFallthroughWhenNotInExperimentVariation()
        {
            var rollout = BuildRollout(RolloutKind.Experiment, true);
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .FallthroughRollout(rollout)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser);

            Assert.Equal(EvaluationReasonKind.Fallthrough, result.Result.Reason.Kind);
            Assert.False(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void FlagReturnsInExperimentForFallthroughWhenInExperimentVariationButNonExperimentRollout()
        {
            var rollout = BuildRollout(RolloutKind.Rollout, false);
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .FallthroughRollout(rollout)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var result = BasicEvaluator.Evaluate(f, baseUser);

            Assert.Equal(EvaluationReasonKind.Fallthrough, result.Result.Reason.Kind);
            Assert.False(result.Result.Reason.InExperiment);
        }

        private static Rollout BuildRollout(RolloutKind kind, bool untrackedVariations)
        {
            var variations = new List<WeightedVariation>()
            {
                new WeightedVariation(1, 50000, untrackedVariations),
                new WeightedVariation(2, 20000, untrackedVariations)
            };
            const int seed = 123;
            return new Rollout(kind, null, seed, variations, new AttributeRef());
        }
    }
}

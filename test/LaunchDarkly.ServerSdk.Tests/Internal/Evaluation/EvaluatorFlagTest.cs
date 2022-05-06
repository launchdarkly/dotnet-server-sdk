using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation at the highest level. Rule-level and clause-level behavior is covered
    // in detail in EvaluatorRuleTest, EvaluatorClauseTest, and EvaluatorSegmentMatchTest.

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
            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
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

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
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

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(offValue, 1,
                EvaluationReason.PrerequisiteFailedReason("feature1"));
            Assert.Equal(expected, result.Result);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("nogo"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
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

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
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

            var result = evaluator.Evaluate(f0, baseUser);

            var expected = new EvaluationDetail<LdValue>(fallthroughValue, 0, EvaluationReason.FallthroughReason);
            Assert.Equal(expected, result.Result);

            Assert.Equal(2, result.PrerequisiteEvals.Count);

            Assert.Collection(result.PrerequisiteEvals,
                e =>
                {
                    Assert.Equal(f2.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f2.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f1.Key, e.PrerequisiteOfFlagKey);
                },
                e =>
                {
                    Assert.Equal(f1.Key, e.PrerequisiteFlag.Key);
                    Assert.Equal(LdValue.Of("go"), e.Result.Value);
                    Assert.Equal(f1.Version, e.PrerequisiteFlag.Version);
                    Assert.Equal(f0.Key, e.PrerequisiteOfFlagKey);
                });
        }
        
        [Fact]
        public void FlagMatchesUserFromTargets()
        {
            var f = new FeatureFlagBuilder("feature")
                .On(true)
                .Targets(new Target(null, new HashSet<string> { "whoever", "userkey" }, 2))
                .FallthroughVariation(0)
                .OffVariation(1)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
            var context = Context.New("userkey");
            var result = BasicEvaluator.Evaluate(f, context);

            var expected = new EvaluationDetail<LdValue>(onValue, 2, EvaluationReason.TargetMatchReason);
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvals.Count);
        }

        private FeatureFlag FeatureFlagWithRules(params FlagRule[] rules)
        {
            return new FeatureFlagBuilder("feature")
                .On(true)
                .Rules(rules)
                .FallthroughVariation(0)
                .Variations(fallthroughValue, offValue, onValue)
                .Build();
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

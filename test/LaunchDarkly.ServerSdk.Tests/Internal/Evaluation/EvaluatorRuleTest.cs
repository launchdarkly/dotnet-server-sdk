using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation at the rule level. Clause-level behavior is covered in detail in
    // EvaluatorClauseTest and EvaluatorSegmentMatchTest.

    public class EvaluatorRuleTest
    {
        private static readonly User baseUser = User.WithKey("userkey");
        private static readonly LdValue fallthroughValue = LdValue.Of("fallthrough");
        private static readonly LdValue offValue = LdValue.Of("off");
        private static readonly LdValue onValue = LdValue.Of("on");

        [Fact]
        public void FlagReturnsInExperimentForRuleMatchWhenInExperimentVariation()
        {
            var user = User.WithKey("userkey");
            var rollout = BuildRollout(RolloutKind.Experiment, false);
            var rule = new RuleBuilder().Id("id").Rollout(rollout).Clauses(ClauseBuilder.ShouldMatchUser(user)).Build();
            var f = FeatureFlagWithRules(rule);
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

            Assert.Equal(EvaluationReasonKind.RuleMatch, result.Result.Reason.Kind);
            Assert.True(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void FlagReturnsNotInExperimentForRuleMatchWhenNotInExperimentVariation()
        {
            var user = User.WithKey("userkey");
            var rollout = BuildRollout(RolloutKind.Experiment, true);
            var rule = new RuleBuilder().Id("id").Rollout(rollout).Clauses(ClauseBuilder.ShouldMatchUser(user)).Build();
            var f = FeatureFlagWithRules(rule);
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

            Assert.Equal(EvaluationReasonKind.RuleMatch, result.Result.Reason.Kind);
            Assert.False(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void FlagReturnsInExperimentForRuleMatchWhenInExperimentVariationButNonExperimentRollout()
        {
            var user = User.WithKey("userkey");
            var rollout = BuildRollout(RolloutKind.Rollout, false);
            var rule = new RuleBuilder().Id("id").Rollout(rollout).Clauses(ClauseBuilder.ShouldMatchUser(user)).Build();
            var f = FeatureFlagWithRules(rule);
            var result = BasicEvaluator.Evaluate(f, baseUser, EventFactory.Default);

            Assert.Equal(EvaluationReasonKind.RuleMatch, result.Result.Reason.Kind);
            Assert.False(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void RuleWithTooHighVariationReturnsMalformedFlagError()
        {
            var user = User.WithKey("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Variation(999).Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
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

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
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

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
        }

        [Fact]
        public void RuleWithRolloutWithEmptyVariationsListReturnsMalformedFlagError()
        {
            var user = User.WithKey("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Clauses(clause)
                .Rollout(new Rollout(RolloutKind.Rollout, null, new List<WeightedVariation>(), null)).Build();
            var f = FeatureFlagWithRules(rule);

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Equal(0, result.PrerequisiteEvents.Count);
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
            return new Rollout(kind, seed, variations, UserAttribute.Key);
        }
    }
}

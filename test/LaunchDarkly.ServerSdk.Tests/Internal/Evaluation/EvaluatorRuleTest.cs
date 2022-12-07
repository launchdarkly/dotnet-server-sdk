using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation at the rule level. Clause-level behavior is covered in detail in
    // EvaluatorClauseTest and EvaluatorSegmentMatchTest.

    public class EvaluatorRuleTest
    {
        private static readonly Context baseUser = Context.New("userkey");
        private static readonly LdValue fallthroughValue = LdValue.Of("fallthrough");
        private static readonly LdValue offValue = LdValue.Of("off");
        private static readonly LdValue onValue = LdValue.Of("on");

        [Fact]
        public void FlagReturnsInExperimentForRuleMatchWhenInExperimentVariation()
        {
            var user = Context.New("userkey");
            var rollout = BuildRollout(RolloutKind.Experiment, false);
            var rule = new RuleBuilder().Id("id").Rollout(rollout).Clauses(ClauseBuilder.ShouldMatchUser(user)).Build();
            var f = FeatureFlagWithRules(rule);
            var result = BasicEvaluator.Evaluate(f, baseUser);

            Assert.Equal(EvaluationReasonKind.RuleMatch, result.Result.Reason.Kind);
            Assert.True(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void FlagReturnsNotInExperimentForRuleMatchWhenNotInExperimentVariation()
        {
            var user = Context.New("userkey");
            var rollout = BuildRollout(RolloutKind.Experiment, true);
            var rule = new RuleBuilder().Id("id").Rollout(rollout).Clauses(ClauseBuilder.ShouldMatchUser(user)).Build();
            var f = FeatureFlagWithRules(rule);
            var result = BasicEvaluator.Evaluate(f, baseUser);

            Assert.Equal(EvaluationReasonKind.RuleMatch, result.Result.Reason.Kind);
            Assert.False(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void FlagReturnsInExperimentForRuleMatchWhenInExperimentVariationButNonExperimentRollout()
        {
            var user = Context.New("userkey");
            var rollout = BuildRollout(RolloutKind.Rollout, false);
            var rule = new RuleBuilder().Id("id").Rollout(rollout).Clauses(ClauseBuilder.ShouldMatchUser(user)).Build();
            var f = FeatureFlagWithRules(rule);
            var result = BasicEvaluator.Evaluate(f, baseUser);

            Assert.Equal(EvaluationReasonKind.RuleMatch, result.Result.Reason.Kind);
            Assert.False(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void InExperimentIsFalseIfContextKindNotFoundForExperiment()
        {
            var context = Context.New(ContextKind.Of("other"), "key");
            var rollout = new Rollout(
                RolloutKind.Experiment,
                ContextKind.Of("nonexistent"),
                null,
                new List<WeightedVariation>()
                {
                    new WeightedVariation(0, 1, false),
                    new WeightedVariation(1, 99999, false)
                },
                AttributeRef.FromLiteral("key")
                );
            var rule = new RuleBuilder().Id("id").Rollout(rollout).Clauses(ClauseBuilder.ShouldMatchAnyContext()).Build();
            var f = FeatureFlagWithRules(rule);
            var result = BasicEvaluator.Evaluate(f, baseUser);

            Assert.Equal(EvaluationReasonKind.RuleMatch, result.Result.Reason.Kind);
            Assert.Equal(0, result.Result.VariationIndex);
            Assert.False(result.Result.Reason.InExperiment);
        }

        [Fact]
        public void RuleWithTooHighVariationReturnsMalformedFlagError()
        {
            var user = Context.New("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Variation(999).Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);

            var result = BasicEvaluator.Evaluate(f, user);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Empty(result.PrerequisiteEvals);
        }

        [Fact]
        public void RuleWithNegativeVariationReturnsMalformedFlagError()
        {
            var user = Context.New("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Variation(-1).Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);

            var result = BasicEvaluator.Evaluate(f, user);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Empty(result.PrerequisiteEvals);
        }

        [Fact]
        public void RuleWithNoVariationOrRolloutReturnsMalformedFlagError()
        {
            var user = Context.New("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);

            var result = BasicEvaluator.Evaluate(f, user);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Empty(result.PrerequisiteEvals);
        }

        [Fact]
        public void RuleWithRolloutWithEmptyVariationsListReturnsMalformedFlagError()
        {
            var user = Context.New("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Clauses(clause)
                .Rollout(new Rollout(RolloutKind.Rollout, null, null, new List<WeightedVariation>(), new AttributeRef())).Build();
            var f = FeatureFlagWithRules(rule);

            var result = BasicEvaluator.Evaluate(f, user);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);
            Assert.Empty(result.PrerequisiteEvals);
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

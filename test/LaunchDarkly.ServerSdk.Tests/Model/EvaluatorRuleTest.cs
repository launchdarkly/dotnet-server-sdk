using System.Collections.Generic;
using LaunchDarkly.Sdk.Internal.Events;
using Xunit;

using static LaunchDarkly.Sdk.Server.Model.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Model
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
        public void RuleWithTooHighVariationReturnsMalformedFlagError()
        {
            var user = User.WithKey("userkey");
            var clause = ClauseBuilder.ShouldMatchUser(user);
            var rule = new RuleBuilder().Id("ruleid").Variation(999).Clauses(clause).Build();
            var f = FeatureFlagWithRules(rule);

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

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

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

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

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

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

            var result = BasicEvaluator.Evaluate(f, user, EventFactory.Default);

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MALFORMED_FLAG));
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

using System.Collections.Generic;
using LaunchDarkly.Sdk.Internal.Events;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Model.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    // Tests of flag evaluation at the segment-matching level.

    public class EvaluatorSegmentMatchTest
    {
        private static readonly User baseUser = User.WithKey("userkey");

        [Fact]
        public void ExplicitIncludeUser()
        {
            var s = new Segment("test", 1, new List<string> { "foo" }, null, null, null, false);
            var u = User.WithKey("foo");
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void ExplicitExcludeUser()
        {
            var s = new Segment("test", 1, null, new List<string> { "foo" }, null, null, false);
            var u = User.WithKey("foo");
            Assert.False(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void ExplicitIncludeHasPrecedence()
        {
            var s = new Segment("test", 1, new List<string> { "foo" }, new List<string> { "foo" }, null, null, false);
            var u = User.WithKey("foo");
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void MatchingRuleWithFullRollout()
        {
            var clause = new ClauseBuilder().Attribute("email").Op("in").Values(LdValue.Of("test@example.com")).Build();
            var rule = new SegmentRule(new List<Clause> { clause }, 100000, null);
            var s = new Segment("test", 1, null, null, null, new List<SegmentRule> { rule }, false);
            var u = User.Builder("foo").Email("test@example.com").Build();
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void MatchingRuleWithZeroRollout()
        {
            var clause = new ClauseBuilder().Attribute("email").Op("in").Values(LdValue.Of("test@example.com")).Build();
            var rule = new SegmentRule(new List<Clause> { clause }, 0, null);
            var s = new Segment("test", 1, null, null, null, new List<SegmentRule> { rule }, false);
            var u = User.Builder("foo").Email("test@example.com").Build();
            Assert.False(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void MatchingRuleWithMultipleClauses()
        {
            var clause1 = new ClauseBuilder().Attribute("email").Op("in").Values(LdValue.Of("test@example.com")).Build();
            var clause2 = new ClauseBuilder().Attribute("name").Op("in").Values(LdValue.Of("bob")).Build();
            var rule = new SegmentRule(new List<Clause> { clause1, clause2 }, null, null);
            var s = new Segment("test", 1, null, null, null, new List<SegmentRule> { rule }, false);
            var u = User.Builder("foo").Email("test@example.com").Name("bob").Build();
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void NonMatchingRuleWithMultipleClauses()
        {
            var clause1 = new ClauseBuilder().Attribute("email").Op("in").Values(LdValue.Of("test@example.com")).Build();
            var clause2 = new ClauseBuilder().Attribute("name").Op("in").Values(LdValue.Of("bill")).Build();
            var rule = new SegmentRule(new List<Clause> { clause1, clause2 }, null, null);
            var s = new Segment("test", 1, null, null, null, new List<SegmentRule> { rule }, false);
            var u = User.Builder("foo").Email("test@example.com").Name("bob").Build();
            Assert.False(SegmentMatchesUser(s, u));
        }

        private bool SegmentMatchesUser(Segment segment, User user)
        {
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment);
            var result = evaluator.Evaluate(flag, user, EventFactory.Default);
            return result.Result.Value.AsBool;
        }
    }
}

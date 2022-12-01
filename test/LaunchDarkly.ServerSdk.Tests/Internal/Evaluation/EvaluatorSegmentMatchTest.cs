using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation at the segment-matching level.

    public class EvaluatorSegmentMatchTest
    {
        [Fact]
        public void ExplicitIncludeUser()
        {
            var s = new SegmentBuilder("test").Version(1).Included("foo").Build();
            var u = Context.New("foo");
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void ExplicitIncludeByContextKind()
        {
            var s = new SegmentBuilder("test").Version(1).
                IncludedContext(kind1, "key1").IncludedContext(kind2, "key2").Build();
            
            Assert.True(SegmentMatchesUser(s, Context.New(kind1, "key1")));
            Assert.True(SegmentMatchesUser(s, Context.New(kind2, "key2")));
            Assert.False(SegmentMatchesUser(s, Context.New(kind1, "key2")));
            Assert.False(SegmentMatchesUser(s, Context.New(kind2, "key1")));
            Assert.False(SegmentMatchesUser(s, Context.New("key1")));
        }

        [Fact]
        public void BasicRuleMatchUser()
        {
            var s = new SegmentBuilder("test").Version(1).
                Rules(new SegmentRuleBuilder().Clauses(ClauseBuilder.ShouldMatchAnyUser()).Build()).
                Build();

            Assert.True(SegmentMatchesUser(s, Context.New("key1")));
            Assert.True(SegmentMatchesUser(s, Context.New("key2")));
        }

        [Fact]
        public void BasicRuleMatchByContextKind()
        {
            var s = new SegmentBuilder("test").Version(1).
                Rules(new SegmentRuleBuilder().Clauses(
                    new ClauseBuilder().ContextKind("kind1").Attribute("key").Op("in").Values("foo").Build()
                    ).Build()).
                Build();

            Assert.True(SegmentMatchesUser(s, Context.New(kind1, "foo")));
            Assert.False(SegmentMatchesUser(s, Context.New(kind2, "foo")));
        }

        [Fact]
        public void ExplicitExcludeUser()
        {
            var s = new SegmentBuilder("test").Version(1).
                Excluded("foo").
                Rules(new SegmentRuleBuilder().Clauses(ClauseBuilder.ShouldMatchAnyUser()).Build()).
                Build();

            Assert.False(SegmentMatchesUser(s, Context.New("foo")));
            Assert.True(SegmentMatchesUser(s, Context.New("bar")));
        }

        [Fact]
        public void ExplicitExcludeByContextKind()
        {
            var s = new SegmentBuilder("test").Version(1).
                ExcludedContext(kind1, "key1").
                Rules(new SegmentRuleBuilder().Clauses(ClauseBuilder.ShouldMatchAnyContext()).Build()).
                Build();

            Assert.False(SegmentMatchesUser(s, Context.New(kind1, "key1")));
            Assert.True(SegmentMatchesUser(s, Context.New(kind1, "key2")));
            Assert.True(SegmentMatchesUser(s, Context.New(kind2, "key1")));
            Assert.True(SegmentMatchesUser(s, Context.New("key1")));
        }

        [Fact]
        public void ExplicitIncludeHasPrecedence()
        {
            var s = new SegmentBuilder("test").Version(1).Included("foo").Excluded("foo").Build();
            var u = Context.New("foo");
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void MatchingRuleWithFullRollout()
        {
            var clause = new ClauseBuilder().Attribute("email").Op("in").Values("test@example.com").Build();
            var s = new SegmentBuilder("test").Version(1)
                .Rules(new SegmentRuleBuilder().Clauses(clause).Weight(100000).Build())
                .Build();
            var u = Context.Builder("foo").Set("email", "test@example.com").Build();
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void MatchingRuleWithZeroRollout()
        {
            var clause = new ClauseBuilder().Attribute("email").Op("in").Values("test@example.com").Build();
            var s = new SegmentBuilder("test").Version(1)
                .Rules(new SegmentRuleBuilder().Clauses(clause).Weight(0).Build())
                .Build();
            var u = Context.Builder("foo").Set("email", "test@example.com").Build();
            Assert.False(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void MatchingRuleWithMultipleClauses()
        {
            var clause1 = new ClauseBuilder().Attribute("email").Op("in").Values("test@example.com").Build();
            var clause2 = new ClauseBuilder().Attribute("name").Op("in").Values("bob").Build();
            var s = new SegmentBuilder("test").Version(1)
                .Rules(new SegmentRuleBuilder().Clauses(clause1, clause2).Build())
                .Build();
            var u = Context.Builder("foo").Set("email", "test@example.com").Name("bob").Build();
            Assert.True(SegmentMatchesUser(s, u));
        }

        [Fact]
        public void NonMatchingRuleWithMultipleClauses()
        {
            var clause1 = new ClauseBuilder().Attribute("email").Op("in").Values("test@example.com").Build();
            var clause2 = new ClauseBuilder().Attribute("name").Op("in").Values("bill").Build();
            var s = new SegmentBuilder("test").Version(1)
                .Rules(new SegmentRuleBuilder().Clauses(clause1, clause2).Build())
                .Build();
            var u = Context.Builder("foo").Set("email", "test@example.com").Name("bob").Build();
            Assert.False(SegmentMatchesUser(s, u));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SegmentCycleDetection(bool cycleGoesToOriginalSegment)
        {
            var segment0 = new SegmentBuilder("segmentkey0")
                .Rules(new SegmentRuleBuilder().Clauses(ClauseBuilder.ShouldMatchSegment("segmentkey1")).Build())
                .Build();
            var segment1 = new SegmentBuilder("segmentkey1")
                .Rules(new SegmentRuleBuilder().Clauses(ClauseBuilder.ShouldMatchSegment("segmentkey2")).Build())
                .Build();

            var cycleTargetKey = cycleGoesToOriginalSegment ? segment0.Key : segment1.Key;
            var segment2 = new SegmentBuilder("segmentkey2")
                .Rules(new SegmentRuleBuilder().Clauses(ClauseBuilder.ShouldMatchSegment(cycleTargetKey)).Build())
                .Build();

            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment0.Key).Build();
            var logCapture = Logs.Capture();
            var evaluator = BasicEvaluator.WithStoredSegments(segment0, segment1, segment2).WithLogger(logCapture.Logger(""));
            var result = evaluator.Evaluate(flag, Context.New("key"));

            var expected = new EvaluationDetail<LdValue>(LdValue.Null, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag));
            Assert.Equal(expected, result.Result);

            AssertHelpers.LogMessageRegex(logCapture, true, LogLevel.Error, ".*segment rule.* circular reference");
        }

        private bool SegmentMatchesUser(Segment segment, Context context)
        {
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment);
            var result = evaluator.Evaluate(flag, context);
            return result.Result.Value.AsBool;
        }
    }
}

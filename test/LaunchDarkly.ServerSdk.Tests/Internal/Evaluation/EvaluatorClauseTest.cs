using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation at the clause level.

    public class EvaluatorClauseTest
    {
        private static readonly User baseUser = User.WithKey("userkey");
        
        [Fact]
        public void ClauseCanMatchBuiltInAttribute()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("in").Values(LdValue.Of("Bob")).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = User.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(true), BasicEvaluator.Evaluate(f, user, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseCanMatchCustomAttribute()
        {
            var clause = new ClauseBuilder().Attribute("legs").Op("in").Values(LdValue.Of(4)).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = User.Builder("key").Custom("legs", 4).Build();

            Assert.Equal(LdValue.Of(true), BasicEvaluator.Evaluate(f, user, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseReturnsFalseForMissingAttribute()
        {
            var clause = new ClauseBuilder().Attribute("legs").Op("in").Values(LdValue.Of(4)).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = User.Builder("key").Name("bob").Build();

            Assert.Equal(LdValue.Of(false), BasicEvaluator.Evaluate(f, user, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseCanBeNegated()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("in").Values(LdValue.Of("Bob"))
                .Negate(true).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = User.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(false), BasicEvaluator.Evaluate(f, user, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void ClauseWithUnknownOperatorDoesNotMatch()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("invalidOp").Values(LdValue.Of("Bob")).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = User.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(false), BasicEvaluator.Evaluate(f, user, EventFactory.Default).Result.Value);
        }
        
        [Fact]
        public void SegmentMatchClauseRetrievesSegmentFromStore()
        {
            var segment = new SegmentBuilder("segkey").Version(1).Included("foo").Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment);

            var f = new FeatureFlagBuilder("key").BooleanMatchingSegment("segkey").Build();
            var user = User.WithKey("foo");

            Assert.Equal(LdValue.Of(true), evaluator.Evaluate(f, user, EventFactory.Default).Result.Value);
        }

        [Fact]
        public void SegmentMatchClauseFallsThroughIfSegmentNotFound()
        {
            var f = new FeatureFlagBuilder("key").BooleanMatchingSegment("segkey").Build();
            var user = User.WithKey("foo");
            var evaluator = BasicEvaluator.WithNonexistentSegment("segkey");

            Assert.Equal(LdValue.Of(false), evaluator.Evaluate(f, user, EventFactory.Default).Result.Value);
        }
    }
}

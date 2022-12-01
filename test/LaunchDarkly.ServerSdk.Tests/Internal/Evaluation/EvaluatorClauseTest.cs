using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Tests of flag evaluation at the clause level.

    public class EvaluatorClauseTest
    {
        private static readonly Context baseUser = Context.New("userkey");
        
        [Fact]
        public void ClauseCanMatchBuiltInAttribute()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("in").Values("Bob").Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = Context.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(true), BasicEvaluator.Evaluate(f, user).Result.Value);
        }

        [Fact]
        public void ClauseCanMatchCustomAttribute()
        {
            var clause = new ClauseBuilder().Attribute("legs").Op("in").Values(4).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = Context.Builder("key").Set("legs", 4).Build();

            Assert.Equal(LdValue.Of(true), BasicEvaluator.Evaluate(f, user).Result.Value);
        }

        [Fact]
        public void ClauseReturnsFalseForMissingAttribute()
        {
            var clause = new ClauseBuilder().Attribute("legs").Op("in").Values(4).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = Context.Builder("key").Name("bob").Build();

            Assert.Equal(LdValue.Of(false), BasicEvaluator.Evaluate(f, user).Result.Value);
        }

        [Fact]
        public void ClauseReturnsMalformedFlagErrorForAttributeNotSpecified()
        {
            var clause = new ClauseBuilder().Op("in").Values(4).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = Context.Builder("key").Name("bob").Build();

            var result = BasicEvaluator.Evaluate(f, user).Result;
            Assert.Equal(EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag), result.Reason);
            Assert.Equal(LdValue.Null, result.Value);
        }

        [Fact]
        public void ClauseReturnsMalformedFlagErrorForMalformedAttributeReference()
        {
            var clause = new ClauseBuilder().Attribute(AttributeRef.FromPath("///")).Op("in").Values(4).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = Context.Builder("key").Name("bob").Build();

            var result = BasicEvaluator.Evaluate(f, user).Result;
            Assert.Equal(EvaluationReason.ErrorReason(EvaluationErrorKind.MalformedFlag), result.Reason);
            Assert.Equal(LdValue.Null, result.Value);
        }

        [Fact]
        public void ClauseCanBeNegated()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("in").Values("Bob")
                .Negate(true).Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = Context.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(false), BasicEvaluator.Evaluate(f, user).Result.Value);
        }

        [Fact]
        public void ClauseWithUnknownOperatorDoesNotMatch()
        {
            var clause = new ClauseBuilder().Attribute("name").Op("invalidOp").Values("Bob").Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var user = Context.Builder("key").Name("Bob").Build();

            Assert.Equal(LdValue.Of(false), BasicEvaluator.Evaluate(f, user).Result.Value);
        }

        [Fact]
        public void ClauseMatchUsesContextKind()
        {
            var clause = new ClauseBuilder().ContextKind("company").Attribute("name").Op("in").Values("Catco").Build();
            var f = new FeatureFlagBuilder("key").BooleanWithClauses(clause).Build();
            var context1 = Context.Builder("cc").Kind("company").Name("Catco").Build();
            var context2 = Context.Builder("l").Name("Lucy").Build();
            var context3 = Context.NewMulti(context1, context2);

            Assert.Equal(LdValue.Of(true), BasicEvaluator.Evaluate(f, context1).Result.Value);
            Assert.Equal(LdValue.Of(false), BasicEvaluator.Evaluate(f, context2).Result.Value);
            Assert.Equal(LdValue.Of(true), BasicEvaluator.Evaluate(f, context3).Result.Value);
        }

        [Fact]
        public void SegmentMatchClauseRetrievesSegmentFromStore()
        {
            var segment = new SegmentBuilder("segkey").Version(1).Included("foo").Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment);

            var f = new FeatureFlagBuilder("key").BooleanMatchingSegment("segkey").Build();
            var user = Context.New("foo");

            Assert.Equal(LdValue.Of(true), evaluator.Evaluate(f, user).Result.Value);
        }

        [Fact]
        public void SegmentMatchClauseFallsThroughIfSegmentNotFound()
        {
            var f = new FeatureFlagBuilder("key").BooleanMatchingSegment("segkey").Build();
            var user = Context.New("foo");
            var evaluator = BasicEvaluator.WithNonexistentSegment("segkey");

            Assert.Equal(LdValue.Of(false), evaluator.Evaluate(f, user).Result.Value);
        }
    }
}

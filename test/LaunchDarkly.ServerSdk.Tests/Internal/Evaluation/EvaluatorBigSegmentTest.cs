using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;
using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTestUtil;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    public class EvaluatorBigSegmentTest
    {
        private static readonly User baseUser = User.WithKey("userkey");

        [Fact]
        public void BigSegmentWithNoProviderIsNotMatched()
        {
            var segment = new SegmentBuilder("segmentkey").Unbounded(true).Generation(1)
                .Included(baseUser.Key) // Included should be ignored for a Big Segment
                .Build();
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment);

            var result = evaluator.Evaluate(flag, baseUser, EventFactory.Default);

            Assert.Equal(LdValue.Of(false), result.Result.Value);
            Assert.Equal(BigSegmentsStatus.NotConfigured, result.Result.Reason.BigSegmentsStatus);
        }

        [Fact]
        public void BigSegmentWithNoGenerationIsNotMatched()
        {
            var segment = new SegmentBuilder("segmentkey").Unbounded(true) // didn't set Generation
                .Build();
            var bigSegments = new MockBigSegmentProvider();
            bigSegments.Membership[baseUser.Key] = MockMembership.New().Include(segment);
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment).WithBigSegments(bigSegments);

            var result = evaluator.Evaluate(flag, baseUser, EventFactory.Default);

            Assert.Equal(LdValue.Of(false), result.Result.Value);
            Assert.Equal(BigSegmentsStatus.NotConfigured, result.Result.Reason.BigSegmentsStatus);
        }

        [Fact]
        public void MatchedWithInclude()
        {
            var segment = new SegmentBuilder("segmentkey").Unbounded(true).Generation(2).Build();
            var bigSegments = new MockBigSegmentProvider();
            bigSegments.Membership[baseUser.Key] = MockMembership.New().Include(segment);
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment).WithBigSegments(bigSegments);

            var result = evaluator.Evaluate(flag, baseUser, EventFactory.Default);

            Assert.Equal(LdValue.Of(true), result.Result.Value);
            Assert.Equal(BigSegmentsStatus.Healthy, result.Result.Reason.BigSegmentsStatus);
        }

        [Fact]
        public void MatchedWithRule()
        {
            var clause = ClauseBuilder.ShouldMatchUser(baseUser);
            var rule = new SegmentRule(new List<Clause> { clause }, null, null);
            var segment = new SegmentBuilder("segmentkey").Unbounded(true).Generation(2)
                .Rules(rule)
                .Build();
            var bigSegments = new MockBigSegmentProvider();
            bigSegments.Membership[baseUser.Key] = MockMembership.New();
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment).WithBigSegments(bigSegments);

            var result = evaluator.Evaluate(flag, baseUser, EventFactory.Default);

            Assert.Equal(LdValue.Of(true), result.Result.Value);
            Assert.Equal(BigSegmentsStatus.Healthy, result.Result.Reason.BigSegmentsStatus);
        }

        [Fact]
        public void UnmatchedByExcludeRegardlessOfRule()
        {
            var clause = ClauseBuilder.ShouldMatchUser(baseUser);
            var rule = new SegmentRule(new List<Clause> { clause }, 0, null);
            var segment = new SegmentBuilder("segmentkey").Unbounded(true).Generation(2)
                .Rules(rule)
                .Build();
            var bigSegments = new MockBigSegmentProvider();
            bigSegments.Membership[baseUser.Key] = MockMembership.New().Exclude(segment);
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment).WithBigSegments(bigSegments);

            var result = evaluator.Evaluate(flag, baseUser, EventFactory.Default);

            Assert.Equal(LdValue.Of(false), result.Result.Value);
            Assert.Equal(BigSegmentsStatus.Healthy, result.Result.Reason.BigSegmentsStatus);
        }

        [Fact]
        public void BigSegmentStatusIsReturnedFromProvider()
        {
            var segment = new SegmentBuilder("segmentkey").Unbounded(true).Generation(2).Build();
            var bigSegments = new MockBigSegmentProvider();
            bigSegments.Status = BigSegmentsStatus.Stale;
            bigSegments.Membership[baseUser.Key] = MockMembership.New().Include(segment);
            var flag = new FeatureFlagBuilder("key").BooleanMatchingSegment(segment.Key).Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment).WithBigSegments(bigSegments);

            var result = evaluator.Evaluate(flag, baseUser, EventFactory.Default);

            Assert.Equal(LdValue.Of(true), result.Result.Value);
            Assert.Equal(BigSegmentsStatus.Stale, result.Result.Reason.BigSegmentsStatus);
        }

        [Fact]
        public void BigSegmentStateIsQueriedOnlyOncePerUserEvenIfFlagReferencesMultipleSegments()
        {
            var segment1 = new SegmentBuilder("segmentkey1").Unbounded(true).Generation(2).Build();
            var segment2 = new SegmentBuilder("segmentkey2").Unbounded(true).Generation(3).Build();
            var bigSegments = new MockBigSegmentProvider();
            var membership = MockMembership.New().Include(segment2);
            bigSegments.Membership[baseUser.Key] = membership;
            var flag = new FeatureFlagBuilder("key").On(true)
                .Variations(LdValue.Of(false), LdValue.Of(true))
                .FallthroughVariation(0)
                .Rules(
                    new RuleBuilder().Variation(1).Clauses(ClauseBuilder.ShouldMatchSegment(segment1.Key)).Build(),
                    new RuleBuilder().Variation(1).Clauses(ClauseBuilder.ShouldMatchSegment(segment2.Key)).Build()
                )
                .Build();
            var evaluator = BasicEvaluator.WithStoredSegments(segment1, segment2).WithBigSegments(bigSegments);

            var result = evaluator.Evaluate(flag, baseUser, EventFactory.Default);

            Assert.Equal(LdValue.Of(true), result.Result.Value);
            Assert.Equal(BigSegmentsStatus.Healthy, result.Result.Reason.BigSegmentsStatus);

            Assert.Equal(1, bigSegments.MembershipQueryCount);
            Assert.Equal(new List<string> { MakeBigSegmentRef(segment1), MakeBigSegmentRef(segment2) },
                membership.Queries);
        }

        private sealed class MockMembership : IMembership
        {
            public List<string> Queries = new List<string>();

            private readonly Func<string, bool?> _fn;

            private MockMembership(Func<string, bool?> fn)
            {
                _fn = fn;
            }

            public static MockMembership New() => new MockMembership(_ => null);

            public bool? CheckMembership(string segmentRef)
            {
                Queries.Add(segmentRef);
                return _fn(segmentRef);
            }

            public MockMembership Include(Segment s) =>
                new MockMembership(segmentRef =>
                    segmentRef == MakeBigSegmentRef(s) ? true : _fn(segmentRef));

            public MockMembership Exclude(Segment s) =>
                new MockMembership(segmentRef =>
                    segmentRef == MakeBigSegmentRef(s) ? false : _fn(segmentRef));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Shortcuts for constructing an Evaluator in tests.

    internal static class EvaluatorTestUtil
    {
        /// <summary>
        /// This Evaluator instance will throw an exception if it tries to query any additional flags or segments.
        /// </summary>
        public static readonly Evaluator BasicEvaluator = new Evaluator(
            flagKey => throw new Exception("Evaluator unexpectedly tried to query flag: " + flagKey),
            segmentKey => throw new Exception("Evaluator unexpectedly tried to query segment: " + segmentKey),
            null,
            TestUtils.NullLogger
        );

        /// <summary>
        /// Decorates an Evaluator instance so that it will be able to query the specified flags. For any other
        /// flags or segments, it will fall back to the base evaluator's behavior.
        /// </summary>
        public static Evaluator WithStoredFlags(this Evaluator baseEvaluator, params FeatureFlag[] flags)
        {
            return new Evaluator(
                flagKey => flags.FirstOrDefault(f => f.Key == flagKey) ?? baseEvaluator.FeatureFlagGetter(flagKey),
                baseEvaluator.SegmentGetter,
                baseEvaluator.BigSegmentsGetter,
                baseEvaluator.Logger
            );
        }

        /// <summary>
        /// Decorates an Evaluator instance so that if it tries to query the specified flag key, it will get a
        /// null (rather than throwing an exception). For any other flags or segments, it will fall back to the
        /// base evaluator's behavior.
        /// </summary>
        public static Evaluator WithNonexistentFlag(this Evaluator baseEvaluator, string nonexistentFlagKey)
        {
            return new Evaluator(
                flagKey => flagKey == nonexistentFlagKey ? null : baseEvaluator.FeatureFlagGetter(flagKey),
                baseEvaluator.SegmentGetter,
                baseEvaluator.BigSegmentsGetter,
                baseEvaluator.Logger
            );
        }

        /// <summary>
        /// Decorates an Evaluator instance so that it will be able to query the specified segments. For any other
        /// flags or segments, it will fall back to the base evaluator's behavior.
        /// </summary>
        public static Evaluator WithStoredSegments(this Evaluator baseEvaluator, params Segment[] segments)
        {
            return new Evaluator(
                baseEvaluator.FeatureFlagGetter,
                segmentKey => segments.FirstOrDefault(s => s.Key == segmentKey) ?? baseEvaluator.SegmentGetter(segmentKey),
                baseEvaluator.BigSegmentsGetter,
                baseEvaluator.Logger
            );
        }

        /// <summary>
        /// Decorates an Evaluator instance so that if it tries to query the specified segment key, it will get a
        /// null (rather than throwing an exception). For any other flags or segments, it will fall back to the
        /// base evaluator's behavior.
        /// </summary>
        public static Evaluator WithNonexistentSegment(this Evaluator baseEvaluator, string nonexistentSegmentKey)
        {
            return new Evaluator(
                baseEvaluator.FeatureFlagGetter,
                segmentKey => segmentKey == nonexistentSegmentKey ? null : baseEvaluator.SegmentGetter(segmentKey),
                baseEvaluator.BigSegmentsGetter,
                baseEvaluator.Logger
            );
        }

        public static Evaluator WithBigSegments(this Evaluator baseEvaluator, MockBigSegmentProvider bigSegments)
        {
            return new Evaluator(
                baseEvaluator.FeatureFlagGetter,
                baseEvaluator.SegmentGetter,
                bigSegments.Query,
                baseEvaluator.Logger
            );
        }

        internal sealed class MockBigSegmentProvider
        {
            public BigSegmentsStatus Status { get; set; } = BigSegmentsStatus.Healthy;
            public int MembershipQueryCount { get; set; } = 0;
            public Dictionary<string, IMembership> Membership { get; } = new Dictionary<string, IMembership>();
            
            public BigSegmentsQueryResult Query(string userKey)
            {
                MembershipQueryCount++;
                if (Membership.TryGetValue(userKey, out var membership))
                {
                    return new BigSegmentsQueryResult { Membership = membership, Status = Status };
                }
                return new BigSegmentsQueryResult { Membership = null, Status = Status };
            }
        }
    }
}

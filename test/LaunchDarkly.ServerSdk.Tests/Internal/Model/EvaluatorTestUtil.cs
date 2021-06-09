using System;
using System.Linq;

namespace LaunchDarkly.Sdk.Server.Internal.Model
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
                baseEvaluator.Logger
            );
        }
    }
}

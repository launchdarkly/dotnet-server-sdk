using System;
using System.Linq;

namespace LaunchDarkly.Sdk.Server.Model
{
    // Shortcuts for constructing an Evaluator in tests.

    internal static class EvaluatorTestUtil
    {
        public static readonly Evaluator BasicEvaluator = new Evaluator(flagKey => null, segmentKey => null);

        public static Evaluator WithStoredFlags(this Evaluator baseEvaluator, params FeatureFlag[] flags)
        {
            return new Evaluator(
                flagKey => flags.FirstOrDefault(f => f.Key == flagKey) ?? baseEvaluator.FeatureFlagGetter(flagKey),
                baseEvaluator.SegmentGetter
            );
        }
        
        public static Evaluator WithStoredSegments(this Evaluator baseEvaluator, params Segment[] segments)
        {
            return new Evaluator(
                baseEvaluator.FeatureFlagGetter,
                segmentKey => segments.FirstOrDefault(s => s.Key == segmentKey) ?? baseEvaluator.SegmentGetter(segmentKey)
            );
        }
    }
}

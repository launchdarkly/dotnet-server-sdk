using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;

using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;
using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTypes;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    internal partial class Evaluator
    {

        private bool MatchSegment(ref EvalState state, in Segment segment)
        {
            if (state.SegmentKeyStack.Contains(segment.Key))
            {
                Logger.Error("Segment rule referencing segment \"{0}\" caused a circular reference;" +
                    " this is probably a temporary condition due to an incomplete update", segment.Key);
                throw new StopEvaluationException(EvaluationErrorKind.MalformedFlag);
            }
            state.SegmentKeyStack.Push(segment.Key);
            try
            {
                if (segment.Unbounded)
                {
                    var includedOrExcluded = MatchUnboundedSegment(ref state, segment);
                    if (includedOrExcluded.HasValue)
                    {
                        return includedOrExcluded.Value;
                    }
                }
                else
                {
                    if (!segment.Preprocessed.IncludedSet.IsEmpty || !segment.Preprocessed.ExcludedSet.IsEmpty)
                    {
                        if (state.Context.TryGetContextByKind(ContextKind.Default, out var matchContext))
                        {
                            if (segment.Preprocessed.IncludedSet.Contains(matchContext.Key))
                            {
                                return true;
                            }
                            if (segment.Preprocessed.ExcludedSet.Contains(matchContext.Key))
                            {
                                return false;
                            }
                        }
                    }
                    foreach (var target in segment.IncludedContexts)
                    {
                        if (state.Context.TryGetContextByKind(target.ContextKind ?? ContextKind.Default, out var matchContext) &&
                            target.PreprocessedValues.Contains(matchContext.Key))
                        {
                            return true;
                        }
                    }
                    foreach (var target in segment.ExcludedContexts)
                    {
                        if (state.Context.TryGetContextByKind(target.ContextKind ?? ContextKind.Default, out var matchContext) &&
                            target.PreprocessedValues.Contains(matchContext.Key))
                        {
                            return false;
                        }
                    }
                }
                if (segment.Rules != null)
                {
                    foreach (var rule in segment.Rules)
                    {
                        if (MatchSegmentRule(ref state, segment, rule))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            finally
            {
                state.SegmentKeyStack.Pop();
            }
        }

        private bool? MatchUnboundedSegment(ref EvalState state, in Segment segment)
        {
            if (!segment.Generation.HasValue)
            {
                // Big segment queries can only be done if the generation is known. If it's unset,
                // that probably means the data store was populated by an older SDK that doesn't know
                // about the Generation property and therefore dropped it from the JSON data. We'll treat
                // that as a "not configured" condition.
                state.BigSegmentsStatus = BigSegmentsStatus.NotConfigured;
                return false;
            }
            if (!state.Context.TryGetContextByKind(segment.UnboundedContextKind ?? ContextKind.Default, out var matchContext))
            {
                return false;
            }
            var key = matchContext.Key;
            BigSegmentStoreTypes.IMembership membership = null;
            if (state.BigSegmentsMembership is null || !state.BigSegmentsMembership.TryGetValue(key, out membership))
            {
                if (BigSegmentsGetter is null)
                {
                    // the SDK hasn't been configured to be able to use Big Segments
                    state.BigSegmentsStatus = BigSegmentsStatus.NotConfigured;
                }
                else
                {
                    var result = BigSegmentsGetter(key);
                    if (state.BigSegmentsMembership is null)
                    {
                        state.BigSegmentsMembership = new Dictionary<string, BigSegmentStoreTypes.IMembership>();
                    }
                    membership = result.Membership;
                    state.BigSegmentsMembership[key] = membership;
                    state.BigSegmentsStatus = result.Status;
                }
            }
            return membership?.CheckMembership(MakeBigSegmentRef(segment));
        }

        private bool MatchSegmentRule(ref EvalState state, in Segment segment, in SegmentRule segmentRule)
        {
            foreach (var c in segmentRule.Clauses)
            {
                if (!MatchClause(ref state, c))
                {
                    return false;
                }
            }

            // If the Weight is absent, this rule matches
            if (!segmentRule.Weight.HasValue)
            {
                return true;
            }

            // All of the clauses are met. See if the user buckets in
            float bucket = Bucketing.ComputeBucketValue(
                false,
                null,
                state.Context,
                segmentRule.RolloutContextKind,
                segment.Key,
                segmentRule.BucketBy,
                segment.Salt
                );
            float weight = (float)segmentRule.Weight / 100000F;
            return bucket < weight;
        }
    }
}

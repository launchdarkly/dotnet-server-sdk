using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;
using static LaunchDarkly.Sdk.Server.Internal.Evaluation.EvaluatorTypes;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    // Encapsulates the feature flag evaluation logic. The Evaluator has no knowledge of the rest of the SDK environment;
    // if it needs to retrieve flags or segments that are referenced by a flag, it does so through a function that is
    // provided in the constructor. It also produces feature requests as appropriate for any referenced prerequisite
    // flags, but does not send them.

    // Implementation note: There are many places where we're iterating over a list and matching against a predicate, etc.
    // There was a deliberate choice *not* to use System.Linq extension methods for this, because whenever you declare a
    // closure that makes any use of variables from outside the lambda expression (including properties of "this"), it has
    // to allocate that lambda on the heap rather than optimizing it away. Flag evaluation is potentially a hot code path,
    // so we'd like to minimize heap churn from things like that; hence we're avoiding lambdas here, and also using
    // structs rather than classes for intermediate state.

    internal sealed partial class Evaluator
    {
        // To allow us to test our error handling with a real client instance instead of mock components,
        // this magic flag key value will cause an exception from Evaluate.
        internal const string FlagKeyToTriggerErrorForTesting = "$ deliberately invalid flag $";
        internal const string ErrorMessageForTesting = "deliberate error for testing";

        internal readonly Func<string, FeatureFlag> FeatureFlagGetter;
        internal readonly Func<string, Segment> SegmentGetter;
        internal readonly Func<string, BigSegmentsQueryResult> BigSegmentsGetter;
        internal readonly Logger Logger;

        // EvalState is a container for mutable state information and immutable parameters whose scope is a
        // single call to Evaluator.Evaluate(). The flag being evaluated is *not* part of the state-- we pass
        // it around as a parameter-- because  a single Evaluate may cause multiple flags to be evaluated due
        // to prerequisite relationships. But the Context is part of the state, because it is always the same
        // no matter how many nested things are being evaluated.
        internal struct EvalState
        {
            internal readonly Context Context;
            internal LazyStack<string> PrereqFlagKeyStack;
            internal ImmutableList<PrerequisiteEvalRecord>.Builder PrereqEvals;
            internal IMembership BigSegmentsMembership;
            internal BigSegmentsStatus? BigSegmentsStatus;

            internal EvalState(Context context)
            {
                Context = context;
                PrereqFlagKeyStack = new LazyStack<string>();
                PrereqEvals = null;
                BigSegmentsMembership = null;
                BigSegmentsStatus = null;
            }
        }


        /// <summary>
        /// Constructs a new Evaluator.
        /// </summary>
        /// <param name="featureFlagGetter">a function that returns the stored FeatureFlag for a given key, or null if not found</param>
        /// <param name="segmentGetter">a function that returns the stored Segment for a given key, or null if not found </param>
        /// <param name="bigSegmentsGetter">a function that queries the Big Segments state for a user key, or null if not available</param>
        /// <param name="logger">log messages will be sent here</param>
        internal Evaluator(Func<string, FeatureFlag> featureFlagGetter,
            Func<string, Segment> segmentGetter,
            Func<string, BigSegmentsQueryResult> bigSegmentsGetter,
            Logger logger)
        {
            FeatureFlagGetter = featureFlagGetter;
            SegmentGetter = segmentGetter;
            BigSegmentsGetter = bigSegmentsGetter;
            Logger = logger;
        }

        /// <summary>
        /// Evaluates a feature flag for a given user.
        /// </summary>
        /// <param name="flag">the flag; must not be null</param>
        /// <param name="context">the evaluation context</param>
        /// <returns>an <see cref="EvalResult"/> containing the evaluation result as well as any events that were produced;
        /// the PrerequisiteEvents list will always be non-null</returns>
        public EvalResult Evaluate(in FeatureFlag flag, in Context context)
        {
            if (flag.Key == FlagKeyToTriggerErrorForTesting)
            {
                throw new Exception(ErrorMessageForTesting);
            }
            if (!context.Valid)
            {
                Logger.Warn("Tried to evaluate flag with invalid context: {0} returning null",
                    flag.Key);

                return new EvalResult(
                    new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified)),
                    ImmutableList.Create<PrerequisiteEvalRecord>());
            }

            try
            {
                var state = new EvalState(context);
                var details = EvaluateInternal(ref state, flag);
                if (state.BigSegmentsStatus.HasValue)
                {
                    details = new EvaluationDetail<LdValue>(
                        details.Value,
                        details.VariationIndex,
                        details.Reason.WithBigSegmentsStatus(state.BigSegmentsStatus.Value)
                        );
                }
                return new EvalResult(details, state.PrereqEvals is null ?
                    ImmutableList.Create<PrerequisiteEvalRecord>() : state.PrereqEvals.ToImmutable());
            }
            catch (Exception e)
            {
                var errorKind = e is StopEvaluationException se ? se.ErrorKind : EvaluationErrorKind.Exception;
                return new EvalResult(ErrorResult(errorKind), ImmutableList.Create<PrerequisiteEvalRecord>());
            }
        }

        private EvaluationDetail<LdValue> EvaluateInternal(ref EvalState state, FeatureFlag flag)
        {
            if (!flag.On)
            {
                return GetOffValue(flag, EvaluationReason.OffReason);
            }

            var prereqFailureReason = CheckPrerequisites(ref state, flag);
            if (prereqFailureReason.HasValue)
            {
                return GetOffValue(flag, prereqFailureReason.Value);
            }

            // Check to see if targets match
            var targetMatchVar = MatchTargets(ref state, flag);
            if (targetMatchVar.HasValue)
            {
                return GetVariation(flag, targetMatchVar.Value, EvaluationReason.TargetMatchReason);
            }

            // Now walk through the rules and see if any match
            var ruleIndex = 0;
            foreach (var rule in flag.Rules)
            {
                if (MatchRule(ref state, rule))
                {
                    return GetValueForVariationOrRollout(ref state, flag, rule.Variation, rule.Rollout,
                        EvaluationReason.RuleMatchReason(ruleIndex, rule.Id));
                }
                ruleIndex++;
            }
            // Walk through the fallthrough and see if it matches
            return GetValueForVariationOrRollout(ref state, flag, flag.Fallthrough.Variation, flag.Fallthrough.Rollout,
                EvaluationReason.FallthroughReason);
        }

        private static EvaluationDetail<LdValue> ErrorResult(EvaluationErrorKind kind) =>
            new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.ErrorReason(kind));

        private EvaluationDetail<LdValue> GetVariation(FeatureFlag flag, int variation, in EvaluationReason reason)
        {
            if (variation < 0 || variation >= flag.Variations.Count())
            {
                Logger.Error("Data inconsistency in feature flag \"{0}\": invalid variation index", flag.Key);
                return ErrorResult(EvaluationErrorKind.MalformedFlag);
            }
            return new EvaluationDetail<LdValue>(flag.Variations.ElementAt(variation), variation, reason);
        }

        private EvaluationDetail<LdValue> GetOffValue(FeatureFlag flag, in EvaluationReason reason)
        {
            if (flag.OffVariation is null) // off variation unspecified - return default value
            {
                return new EvaluationDetail<LdValue>(LdValue.Null, null, reason);
            }
            return GetVariation(flag, flag.OffVariation.Value, reason);
        }

        // Checks prerequisites if any; returns null if successful, or an EvaluationReason if we have to
        // short-circuit due to a prerequisite failure. May add events to _prereqEvents.
        private EvaluationReason? CheckPrerequisites(ref EvalState state, FeatureFlag flag)
        {
            state.PrereqFlagKeyStack.Push(flag.Key);
            try
            {
                foreach (var prereq in flag.Prerequisites)
                {
                    if (state.PrereqFlagKeyStack.Contains(prereq.Key))
                    {
                        Logger.Error("Prerequisite relationship to {0} caused a circular reference;" +
                            " this is probably a temporary condition due to an incomplete update", prereq.Key);
                        throw new StopEvaluationException(EvaluationErrorKind.MalformedFlag);
                    }
                    var prereqOk = true;
                    var prereqFeatureFlag = FeatureFlagGetter(prereq.Key);
                    if (prereqFeatureFlag == null)
                    {
                        Logger.Error("Could not retrieve prerequisite flag \"{0}\" when evaluating \"{1}\"",
                            prereq.Key, flag.Key);
                        prereqOk = false;
                    }
                    else
                    {
                        var prereqDetails = EvaluateInternal(ref state, prereqFeatureFlag);
                        // Note that if the prerequisite flag is off, we don't consider it a match no matter
                        // what its off variation was. But we still need to evaluate it in order to generate
                        // an event.
                        if (!prereqFeatureFlag.On || prereqDetails.VariationIndex == null || prereqDetails.VariationIndex.Value != prereq.Variation)
                        {
                            prereqOk = false;
                        }
                        if (state.PrereqEvals is null)
                        {
                            state.PrereqEvals = ImmutableList.CreateBuilder<PrerequisiteEvalRecord>();
                        }
                        state.PrereqEvals.Add(new PrerequisiteEvalRecord(prereqFeatureFlag, flag.Key, prereqDetails));
                    }
                    if (!prereqOk)
                    {
                        return EvaluationReason.PrerequisiteFailedReason(prereq.Key);
                    }
                }
                return null;
            }
            finally
            {
                state.PrereqFlagKeyStack.Pop();
            }
        }

        private EvaluationDetail<LdValue> GetValueForVariationOrRollout(
            ref EvalState state,
            FeatureFlag flag,
            int? variation,
            in Rollout? rollout,
            in EvaluationReason reason
            )
        {
            if (variation.HasValue)
            {
                return GetVariation(flag, variation.Value, reason);
            }

            if (rollout.HasValue && rollout.Value.Variations.Any())
            {
                WeightedVariation? selectedVariation = null;
                float bucket = Bucketing.ComputeBucketValue(
                    rollout.Value.Kind == RolloutKind.Experiment,
                    rollout.Value.Seed,
                    state.Context,
                    rollout.Value.ContextKind,
                    flag.Key,
                    rollout.Value.BucketBy,
                    flag.Salt
                    );
                float sum = 0F;
                foreach (WeightedVariation wv in rollout.Value.Variations)
                {
                    sum += (float)wv.Weight / 100000F;
                    if (bucket < sum)
                    {
                        selectedVariation = wv;
                        break;
                    }
                }
                if (!selectedVariation.HasValue)
                {
                    // The user's bucket value was greater than or equal to the end of the last bucket. This could happen due
                    // to a rounding error, or due to the fact that we are scaling to 100000 rather than 99999, or the flag
                    // data could contain buckets that don't actually add up to 100000. Rather than returning an error in
                    // this case (or changing the scaling, which would potentially change the results for *all* users), we
                    // will simply put the user in the last bucket.
                    selectedVariation = rollout.Value.Variations.Last();
                }
                var inExperiment = (rollout.Value.Kind == RolloutKind.Experiment) && !selectedVariation.Value.Untracked;
                return GetVariation(flag, selectedVariation.Value.Variation,
                    inExperiment ? reason.WithInExperiment(true) : reason);
            }
            else
            {
                Logger.Error("Data inconsistency in feature flag \"{0}\": variation/rollout object with no variation or rollout", flag.Key);
                return ErrorResult(EvaluationErrorKind.MalformedFlag);
            }
        }

        private bool MatchRule(ref EvalState state, in FlagRule rule)
        {
            // A rule matches if ALL of its clauses match
            foreach (var c in rule.Clauses)
            {
                if (!MatchClause(ref state, c))
                {
                    return false;
                }
            }
            return true;
        }

        private bool MatchSegment(ref EvalState state, in Segment segment)
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
                    if (state.Context.TryGetContextByKind(Context.DefaultKind, out var matchContext))
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
                    if (state.Context.TryGetContextByKind(target.ContextKind, out var matchContext) &&
                        target.PreprocessedValues.Contains(matchContext.Key))
                    {
                        return true;
                    }
                }
                foreach (var target in segment.ExcludedContexts)
                {
                    if (state.Context.TryGetContextByKind(target.ContextKind, out var matchContext) &&
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
            // Even if multiple Big Segments are referenced within a single flag evaluation,
            // we only need to do this query once, since it returns *all* of the user's segment
            // memberships.
            if (!state.BigSegmentsStatus.HasValue)
            {
                if (BigSegmentsGetter is null)
                {
                    // the SDK hasn't been configured to be able to use Big Segments
                    state.BigSegmentsStatus = BigSegmentsStatus.NotConfigured;
                }
                else
                {
                    var result = BigSegmentsGetter(state.Context.Key);
                    state.BigSegmentsMembership = result.Membership;
                    state.BigSegmentsStatus = result.Status;
                }
            }
            return state.BigSegmentsMembership is null ? null :
                state.BigSegmentsMembership.CheckMembership(MakeBigSegmentRef(segment));
        }

        private bool MatchSegmentRule(ref EvalState state, in Segment segment, in SegmentRule segmentRule)
        {
            foreach (var c in segmentRule.Clauses)
            {
                if (!MatchClauseNoSegments(ref state, c))
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
                segmentRule.RolloutContextKind ?? Context.DefaultKind,
                segment.Key,
                segmentRule.BucketBy,
                segment.Salt
                );
            float weight = (float)segmentRule.Weight / 100000F;
            return bucket < weight;
        }
    }
}

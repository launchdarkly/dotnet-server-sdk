using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.BigSegmentStoreTypes;
using static LaunchDarkly.Sdk.Server.Interfaces.EventProcessorTypes;
using static LaunchDarkly.Sdk.Server.Internal.BigSegments.BigSegmentsInternalTypes;

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

    internal sealed class Evaluator
    {
        // To allow us to test our error handling with a real client instance instead of mock components,
        // this magic flag key value will cause an exception from Evaluate.
        internal const string FlagKeyToTriggerErrorForTesting = "$ deliberately invalid flag $";
        internal const string ErrorMessageForTesting = "deliberate error for testing";

        private readonly Func<string, FeatureFlag> _featureFlagGetter;
        private readonly Func<string, Segment> _segmentGetter;
        private readonly Func<string, BigSegmentsQueryResult> _bigSegmentsGetter;
        private readonly Logger _logger;

        // exposed for testing
        internal Func<string, FeatureFlag> FeatureFlagGetter => _featureFlagGetter;
        internal Func<string, Segment> SegmentGetter => _segmentGetter;
        internal Func<string, BigSegmentsQueryResult> BigSegmentsGetter => _bigSegmentsGetter;
        internal Logger Logger => _logger;

        internal struct EvalResult
        {
            internal EvaluationDetail<LdValue> Result;
            internal readonly IList<EvaluationEvent> PrerequisiteEvents;

            internal EvalResult(EvaluationDetail<LdValue> result, IList<EvaluationEvent> events)
            {
                Result = result;
                PrerequisiteEvents = events;
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
            _featureFlagGetter = featureFlagGetter;
            _segmentGetter = segmentGetter;
            _bigSegmentsGetter = bigSegmentsGetter;
            _logger = logger;
        }

        /// <summary>
        /// Evaluates a feature flag for a given user.
        /// </summary>
        /// <param name="flag">the flag; must not be null</param>
        /// <param name="user">the user</param>
        /// <param name="eventFactory">an <see cref="EventFactory"/> instance that will be called to produce any necessary
        /// prerequisite flag events</param>
        /// <returns>an <see cref="EvalResult"/> containing the evaluation result as well as any events that were produced;
        /// the PrerequisiteEvents list will always be non-null</returns>
        public EvalResult Evaluate(FeatureFlag flag, User user, EventFactory eventFactory)
        {
            if (flag.Key == FlagKeyToTriggerErrorForTesting)
            {
                throw new Exception(ErrorMessageForTesting);
            }
            if (user == null || user.Key == null)
            {
                _logger.Warn("User or user key is null when evaluating flag: {0} returning null",
                    flag.Key);

                return new EvalResult(
                    new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified)),
                    ImmutableList.Create<EvaluationEvent>());
            }

            var scope = new EvalScope(this, flag, user, eventFactory);
            return scope.Evaluate();
        }

        private struct LazilyCreatedList<T>
        {
            private IList<T> _list;
            
            internal void Add(T item)
            {
                if (_list is null)
                {
                    _list = new List<T>();
                }
                _list.Add(item);
            }
            
            internal IList<T> GetList()
            {
                return _list ?? ImmutableList.Create<T>(); // ImmutableList.Create for an empty list doesn't create a new object
            }
        }

        /// <summary>
        /// Encapsulates the parameters for a single evaluation request, so we don't have to keep passing them around
        /// as parameters within the evaluation logic. This is a value type to avoid allocation overhead.
        /// </summary>
        private struct EvalScope
        {
            private readonly Evaluator _parent;
            private readonly FeatureFlag _flag;
            private readonly User _user;
            private readonly EventFactory _eventFactory;
            private LazilyCreatedList<EvaluationEvent> _prereqEvents;
            private IMembership _bigSegmentsMembership;
            private BigSegmentsStatus? _bigSegmentsStatus;

            internal EvalScope(Evaluator parent, FeatureFlag flag, User user, EventFactory eventFactory)
            {
                _parent = parent;
                _flag = flag;
                _user = user;
                _eventFactory = eventFactory;
                _prereqEvents = new LazilyCreatedList<EvaluationEvent>();
                _bigSegmentsMembership = null;
                _bigSegmentsStatus = null;
            }

            internal EvalResult Evaluate()
            {
                var details = EvaluateInternal();
                if (_bigSegmentsStatus.HasValue)
                {
                    details = new EvaluationDetail<LdValue>(
                        details.Value,
                        details.VariationIndex,
                        details.Reason.WithBigSegmentsStatus(_bigSegmentsStatus.Value)
                        );
                }
                return new EvalResult(details, _prereqEvents.GetList());
            }

            private EvaluationDetail<LdValue> EvaluateInternal()
            {
                if (!_flag.On)
                {
                    return GetOffValue(EvaluationReason.OffReason);
                }

                var prereqFailureReason = CheckPrerequisites();
                if (prereqFailureReason.HasValue)
                {
                    return GetOffValue(prereqFailureReason.Value);
                }

                // Check to see if targets match
                foreach (var target in _flag.Targets)
                {
                    if (target.Preprocessed.ValuesSet.Contains(_user.Key))
                    {
                        return GetVariation(target.Variation, EvaluationReason.TargetMatchReason);
                    }
                }
                // Now walk through the rules and see if any match
                var ruleIndex = 0;
                foreach (var rule in _flag.Rules)
                {
                    if (MatchRule(rule))
                    {
                        return GetValueForVariationOrRollout(rule.Variation, rule.Rollout,
                            EvaluationReason.RuleMatchReason(ruleIndex, rule.Id));
                    }
                    ruleIndex++;
                }
                // Walk through the fallthrough and see if it matches
                return GetValueForVariationOrRollout(_flag.Fallthrough.Variation, _flag.Fallthrough.Rollout,
                    EvaluationReason.FallthroughReason);
            }

            // Checks prerequisites if any; returns null if successful, or an EvaluationReason if we have to
            // short-circuit due to a prerequisite failure. May add events to _prereqEvents.
            private EvaluationReason? CheckPrerequisites()
            {
                foreach (var prereq in _flag.Prerequisites)
                {
                    var prereqOk = true;
                    var prereqFeatureFlag = _parent._featureFlagGetter(prereq.Key);
                    if (prereqFeatureFlag == null)
                    {
                        _parent._logger.Error("Could not retrieve prerequisite flag \"{0}\" when evaluating \"{1}\"",
                            prereq.Key, _flag.Key);
                        prereqOk = false;
                    }
                    else
                    {
                        var prereqScope = new EvalScope(_parent, prereqFeatureFlag, _user, _eventFactory);
                        var prereqEvalResult = prereqScope.Evaluate();
                        var prereqDetails = prereqEvalResult.Result;
                        // Note that if the prerequisite flag is off, we don't consider it a match no matter
                        // what its off variation was. But we still need to evaluate it in order to generate
                        // an event.
                        if (!prereqFeatureFlag.On || prereqDetails.VariationIndex == null || prereqDetails.VariationIndex.Value != prereq.Variation)
                        {
                            prereqOk = false;
                        }
                        foreach (var subPrereqEvent in prereqEvalResult.PrerequisiteEvents)
                        {
                            _prereqEvents.Add(subPrereqEvent);
                        }
                        _prereqEvents.Add(_eventFactory.NewPrerequisiteEvaluationEvent(prereqFeatureFlag, _user,
                            prereqDetails, _flag));
                    }
                    if (!prereqOk)
                    {
                        return EvaluationReason.PrerequisiteFailedReason(prereq.Key);
                    }
                }
                return null;
            }

            private static EvaluationDetail<LdValue> ErrorResult(EvaluationErrorKind kind) =>
                new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.ErrorReason(kind));

            private EvaluationDetail<LdValue> GetVariation(int variation, EvaluationReason reason)
            {
                if (variation < 0 || variation >= _flag.Variations.Count())
                {
                    _parent._logger.Error("Data inconsistency in feature flag \"{0}\": invalid variation index", _flag.Key);
                    return ErrorResult(EvaluationErrorKind.MalformedFlag);
                }
                return new EvaluationDetail<LdValue>(_flag.Variations.ElementAt(variation), variation, reason);
            }

            private EvaluationDetail<LdValue> GetOffValue(EvaluationReason reason)
            {
                if (_flag.OffVariation is null) // off variation unspecified - return default value
                {
                    return new EvaluationDetail<LdValue>(LdValue.Null, null, reason);
                }
                return GetVariation(_flag.OffVariation.Value, reason);
            }

            private EvaluationDetail<LdValue> GetValueForVariationOrRollout(int? variation, Rollout? rollout, EvaluationReason reason)
            {
                if (variation.HasValue)
                {
                    return GetVariation(variation.Value, reason);
                }

                if (rollout.HasValue && rollout.Value.Variations.Count() > 0)
                {
                    WeightedVariation? selectedVariation = null;
                    var bucketBy = rollout.Value.BucketBy.GetValueOrDefault(UserAttribute.Key);
                    float bucket = Bucketing.BucketUser(rollout.Value.Seed, _user, _flag.Key, bucketBy, _flag.Salt);
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
                    return GetVariation(selectedVariation.Value.Variation,
                        inExperiment ? reason.WithInExperiment(true) : reason);
                }
                else
                {
                    _parent._logger.Error("Data inconsistency in feature flag \"{0}\": variation/rollout object with no variation or rollout", _flag.Key);
                    return ErrorResult(EvaluationErrorKind.MalformedFlag);
                }
            }

            private bool MatchRule(FlagRule rule)
            {
                // A rule matches if ALL of its clauses match
                foreach (var c in rule.Clauses)
                {
                    if (!MatchClause(c))
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool MatchClause(Clause clause)
            {
                // A clause matches if ANY of its values match, for the given attribute and operator
                if (clause.Op == Operator.SegmentMatch)
                {
                    foreach (var value in clause.Values)
                    {
                        Segment segment = _parent._segmentGetter(value.AsString);
                        if (segment != null && MatchSegment(segment))
                        {
                            return MaybeNegate(clause, true);
                        }
                    }
                    return MaybeNegate(clause, false);
                }
                else
                {
                    return MatchClauseNoSegments(clause);
                }
            }

            private bool MatchClauseNoSegments(Clause clause)
            {
                var userValue = _user.GetAttribute(clause.Attribute);
                if (userValue.IsNull)
                {
                    return false;
                }
                if (userValue.Type == LdValueType.Array)
                {
                    var list = userValue.AsList(LdValue.Convert.Json);
                    foreach (var element in list)
                    {
                        if (element.Type == LdValueType.Array || element.Type == LdValueType.Object)
                        {
                            _parent._logger.Error("Invalid custom attribute value in user object: {0}",
                                element);
                            return false;
                        }
                        if (ClauseMatchAny(clause, element))
                        {
                            return MaybeNegate(clause, true);
                        }
                    }
                    return MaybeNegate(clause, false);
                }
                else if (userValue.Type == LdValueType.Object)
                {
                    _parent._logger.Warn("Got unexpected user attribute type {0} for user attribute \"{1}\"",
                        userValue.Type,
                        clause.Attribute);
                    return false;
                }
                else
                {
                    return MaybeNegate(clause, ClauseMatchAny(clause, userValue));
                }
            }

            private bool MatchSegment(Segment segment)
            {
                var userKey = _user.Key;
                if (userKey is null)
                {
                    return false;
                }
                if (segment.Unbounded)
                {
                    if (!segment.Generation.HasValue)
                    {
                        // Big segment queries can only be done if the generation is known. If it's unset,
                        // that probably means the data store was populated by an older SDK that doesn't know
                        // about the Generation property and therefore dropped it from the JSON data. We'll treat
                        // that as a "not configured" condition.
                        _bigSegmentsStatus = BigSegmentsStatus.NotConfigured;
                        return false;
                    }
                    // Even if multiple Big Segments are referenced within a single flag evaluation,
                    // we only need to do this query once, since it returns *all* of the user's segment
                    // memberships.
                    if (!_bigSegmentsStatus.HasValue)
                    {
                        if (_parent._bigSegmentsGetter is null)
                        {
                            // the SDK hasn't been configured to be able to use Big Segments
                            _bigSegmentsStatus = BigSegmentsStatus.NotConfigured;
                        }
                        else
                        {
                            var result = _parent._bigSegmentsGetter(userKey);
                            _bigSegmentsMembership = result.Membership;
                            _bigSegmentsStatus = result.Status;
                        }
                    }
                    var included = _bigSegmentsMembership is null ? null :
                        _bigSegmentsMembership.CheckMembership(MakeBigSegmentRef(segment));
                    if (included.HasValue)
                    {
                        return included.Value;
                    }
                }
                else
                {
                    if (segment.Preprocessed.IncludedSet.Contains(userKey))
                    {
                        return true;
                    }
                    if (segment.Preprocessed.ExcludedSet.Contains(userKey))
                    {
                        return false;
                    }
                }
                if (segment.Rules != null)
                {
                    foreach (var rule in segment.Rules)
                    {
                        if (MatchSegmentRule(segment, rule))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private bool MatchSegmentRule(Segment segment, SegmentRule segmentRule)
            {
                foreach (var c in segmentRule.Clauses)
                {
                    if (!MatchClauseNoSegments(c))
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
                var by = segmentRule.BucketBy.GetValueOrDefault(UserAttribute.Key);
                double bucket = Bucketing.BucketUser(null, _user, segment.Key, by, segment.Salt);
                double weight = (double)segmentRule.Weight / 100000F;
                return bucket < weight;
            }
        }

        internal static bool ClauseMatchAny(Clause clause, LdValue userValue)
        {
            // Special case for the "in" operator - we preprocess the values to a set for fast lookup
            if (clause.Op == Operator.In && clause.Preprocessed.ValuesAsSet != null)
            {
                return clause.Preprocessed.ValuesAsSet.Contains(userValue);
            }

            int index = 0;
            foreach (var clauseValue in clause.Values)
            {
                var preprocessedValue = clause.Preprocessed.Values is null ? (Clause.PreprocessedValue?)null :
                    clause.Preprocessed.Values[index++];
                if (clause.Op.Apply(userValue, clauseValue, preprocessedValue))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool MaybeNegate(Clause clause, bool b) =>
            clause.Negate ? !b : b;
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.Model
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
        private readonly Func<string, FeatureFlag> _featureFlagGetter;
        private readonly Func<string, Segment> _segmentGetter;
        private readonly Logger _logger;

        // exposed for testing
        internal Func<string, FeatureFlag> FeatureFlagGetter => _featureFlagGetter;
        internal Func<string, Segment> SegmentGetter => _segmentGetter;
        internal Logger Logger => _logger;

        internal struct EvalResult
        {
            internal EvaluationDetail<LdValue> Result;
            internal readonly IList<FeatureRequestEvent> PrerequisiteEvents;

            internal EvalResult(EvaluationDetail<LdValue> result, IList<FeatureRequestEvent> events)
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
        /// <param name="logger">log messages will be sent here</param>
        internal Evaluator(Func<string, FeatureFlag> featureFlagGetter,
            Func<string, Segment> segmentGetter,
            Logger logger)
        {
            _featureFlagGetter = featureFlagGetter;
            _segmentGetter = segmentGetter;
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
            if (user == null || user.Key == null)
            {
                _logger.Warn("User or user key is null when evaluating flag: {0} returning null",
                    flag.Key);

                return new EvalResult(
                    new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.ErrorReason(EvaluationErrorKind.USER_NOT_SPECIFIED)),
                    ImmutableList.Create<FeatureRequestEvent>());
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
            private LazilyCreatedList<FeatureRequestEvent> _prereqEvents;

            internal EvalScope(Evaluator parent, FeatureFlag flag, User user, EventFactory eventFactory)
            {
                _parent = parent;
                _flag = flag;
                _user = user;
                _eventFactory = eventFactory;
                _prereqEvents = new LazilyCreatedList<FeatureRequestEvent>();
            }

            internal EvalResult Evaluate()
            {
                var details = EvaluateInternal();
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
                if (_flag.Targets != null)
                {
                    foreach (var target in _flag.Targets)
                    {
                        foreach (var v in target.Values)
                        {
                            if (_user.Key == v)
                            {
                                return GetVariation(target.Variation, EvaluationReason.TargetMatchReason);
                            }
                        }
                    }
                }
                // Now walk through the rules and see if any match
                if (_flag.Rules != null)
                {
                    for (int i = 0; i < _flag.Rules.Count; i++)
                    {
                        Rule rule = _flag.Rules[i];
                        if (MatchRule(rule))
                        {
                            return GetValueForVariationOrRollout(rule, EvaluationReason.RuleMatchReason(i, rule.Id));
                        }
                    }
                }
                // Walk through the fallthrough and see if it matches
                if (_flag.Fallthrough is null)
                {
                    return ErrorResult(EvaluationErrorKind.MALFORMED_FLAG);
                }
                return GetValueForVariationOrRollout(_flag.Fallthrough, EvaluationReason.FallthroughReason);
            }

            // Checks prerequisites if any; returns null if successful, or an EvaluationReason if we have to
            // short-circuit due to a prerequisite failure. May add events to _prereqEvents.
            private EvaluationReason? CheckPrerequisites()
            {
                if (_flag.Prerequisites == null || _flag.Prerequisites.Count == 0)
                {
                    return null;
                }
                var parentFlagEventProperties = new FeatureFlagEventProperties(_flag);
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
                        _prereqEvents.Add(_eventFactory.NewPrerequisiteFeatureRequestEvent(new FeatureFlagEventProperties(prereqFeatureFlag), _user,
                            prereqDetails, parentFlagEventProperties));
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
                if (variation < 0 || variation >= _flag.Variations.Count)
                {
                    _parent._logger.Error("Data inconsistency in feature flag \"{0}\": invalid variation index", _flag.Key);
                    return ErrorResult(EvaluationErrorKind.MALFORMED_FLAG);
                }
                return new EvaluationDetail<LdValue>(_flag.Variations[variation], variation, reason);
            }

            private EvaluationDetail<LdValue> GetOffValue(EvaluationReason reason)
            {
                if (_flag.OffVariation is null) // off variation unspecified - return default value
                {
                    return new EvaluationDetail<LdValue>(LdValue.Null, null, reason);
                }
                return GetVariation(_flag.OffVariation.Value, reason);
            }

            private EvaluationDetail<LdValue> GetValueForVariationOrRollout(VariationOrRollout vr, EvaluationReason reason)
            {
                var index = VariationIndexForUser(vr, _flag.Key, _flag.Salt);
                if (index is null)
                {
                    _parent._logger.Error("Data inconsistency in feature flag \"{0}\": variation/rollout object with no variation or rollout", _flag.Key);
                    return ErrorResult(EvaluationErrorKind.MALFORMED_FLAG);
                }
                return GetVariation(index.Value, reason);
            }

            private int? VariationIndexForUser(VariationOrRollout vr, string key, string salt)
            {
                if (vr.Variation.HasValue)
                {
                    return vr.Variation.Value;
                }

                if (vr.Rollout != null &&vr. Rollout.Variations != null && vr.Rollout.Variations.Count > 0)
                {
                    string bucketBy = vr.Rollout.BucketBy ?? "key";
                    float bucket = Bucketing.BucketUser(_user, key, bucketBy, salt);
                    float sum = 0F;
                    foreach (WeightedVariation wv in vr.Rollout.Variations)
                    {
                        sum += (float)wv.Weight / 100000F;
                        if (bucket < sum)
                        {
                            return wv.Variation;
                        }
                    }
                    // The user's bucket value was greater than or equal to the end of the last bucket. This could happen due
                    // to a rounding error, or due to the fact that we are scaling to 100000 rather than 99999, or the flag
                    // data could contain buckets that don't actually add up to 100000. Rather than returning an error in
                    // this case (or changing the scaling, which would potentially change the results for *all* users), we
                    // will simply put the user in the last bucket.
                    return vr.Rollout.Variations[vr.Rollout.Variations.Count - 1].Variation;
                }
                return null;
            }

            private bool MatchRule(Rule rule)
            {
                // A rule matches if ALL of its clauses match
                if (rule.Clauses != null)
                {
                    foreach (var c in rule.Clauses)
                    {
                        if (!MatchClause(c))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            private bool MatchClause(Clause clause)
            {
                // A clause matches if ANY of its values match, for the given attribute and operator
                if (clause.Op == "segmentMatch")
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
                var userValue = Operator.GetUserAttributeForEvaluation(_user, clause.Attribute);
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
                    _parent._logger.Warn("Got unexpected user attribute type: {0} for user key: {1} and attribute: {2}",
                        userValue.Type,
                        _user.Key,
                        clause.Attribute);
                    return false;
                }
                else
                {
                    return MaybeNegate(clause, ClauseMatchAny(clause, userValue));
                }
            }

            private static bool ClauseMatchAny(Clause clause, LdValue userValue)
            {
                foreach (var v in clause.Values)
                {
                    if (Operator.Apply(clause.Op, userValue, v))
                    {
                        return true;
                    }
                }
                return false;
            }

            private static bool MaybeNegate(Clause clause, bool b)
            {
                return clause.Negate ? !b : b;
            }

            private bool MatchSegment(Segment segment)
            {
                var userKey = _user.Key;
                if (userKey != null)
                {
                    if (segment.Included != null && segment.Included.Contains(userKey))
                    {
                        return true;
                    }
                    if (segment.Excluded != null && segment.Excluded.Contains(userKey))
                    {
                        return false;
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
                var by = segmentRule.BucketBy ?? "key";
                double bucket = Bucketing.BucketUser(_user, segment.Key, by, segment.Salt);
                double weight = (double)segmentRule.Weight / 100000F;
                return bucket < weight;
            }
        }
    }
}

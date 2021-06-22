using System.Linq;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Interfaces.EventProcessorTypes;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal sealed class EventFactory
    {
        private readonly bool _withReasons;

        internal static readonly EventFactory Default = new EventFactory(false);
        internal static readonly EventFactory DefaultWithReasons = new EventFactory(true);

        internal EventFactory(bool withReasons)
        {
            _withReasons = withReasons;
        }

        internal EvaluationEvent NewEvaluationEvent(
            FeatureFlag flag,
            User user,
            EvaluationDetail<LdValue> result,
            LdValue defaultValue
            )
        {
            var isExperiment = IsExperiment(flag, result.Reason);
            return new EvaluationEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                User = user,
                FlagKey = flag.Key,
                FlagVersion = flag.Version,
                Variation = result.VariationIndex,
                Value = result.Value,
                Default = defaultValue,
                Reason = (_withReasons || isExperiment) ? result.Reason : (EvaluationReason?)null,
                TrackEvents = flag.TrackEvents || isExperiment,
                DebugEventsUntilDate = flag.DebugEventsUntilDate
            };
        }

        internal EvaluationEvent NewDefaultValueEvaluationEvent(
            FeatureFlag flag,
            User user,
            LdValue defaultValue,
            EvaluationErrorKind errorKind
            )
        {
            return new EvaluationEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                User = user,
                FlagKey = flag.Key,
                FlagVersion = flag.Version,
                Value = defaultValue,
                Default = defaultValue,
                Reason = EvaluationReason.ErrorReason(errorKind),
                TrackEvents = flag.TrackEvents,
                DebugEventsUntilDate = flag.DebugEventsUntilDate
            };
        }

        internal EvaluationEvent NewUnknownFlagEvaluationEvent(
            string flagKey,
            User user,
            LdValue defaultValue,
            EvaluationErrorKind errorKind
            )
        {
            return new EvaluationEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                User = user,
                FlagKey = flagKey,
                Value = defaultValue,
                Default = defaultValue,
                Reason = EvaluationReason.ErrorReason(errorKind)
            };
        }

        internal EvaluationEvent NewPrerequisiteEvaluationEvent(
            FeatureFlag prereqFlagBeingEvaluated,
            User user,
            EvaluationDetail<LdValue> result,
            FeatureFlag flagThatReferencesPrerequisite
            )
        {
            var e = NewEvaluationEvent(prereqFlagBeingEvaluated, user, result, LdValue.Null);
            e.PrerequisiteOf = flagThatReferencesPrerequisite.Key;
            return e;
        }

        private static bool IsExperiment(FeatureFlag flag, EvaluationReason? reason)
        {
            if (!reason.HasValue)
            {
                return false;
            }
            var r = reason.Value;

            // If the reason says we're in an experiment, we are. (That is, r.InExperiment would only
            // have been set to true if the evaluator already determined that this was appropriate.)
            // Otherwise, apply the legacy rule exclusion logic.
            if (r.InExperiment)
            {
                return true;
            }
            switch (r.Kind)
            {
                case EvaluationReasonKind.Fallthrough:
                    return flag.TrackEventsFallthrough;
                case EvaluationReasonKind.RuleMatch:
                    return r.RuleIndex.HasValue && r.RuleIndex >= 0 && r.RuleIndex < flag.Rules.Count() &&
                        flag.Rules.ElementAt(r.RuleIndex.Value).TrackEvents;
            }
            return false;
        }
    }
}

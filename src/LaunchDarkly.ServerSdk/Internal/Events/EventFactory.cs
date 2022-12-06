using System.Linq;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Server.Subsystems.EventProcessorTypes;

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
            Context context,
            EvaluationDetail<LdValue> result,
            LdValue defaultValue
            )
        {
            var isExperiment = IsExperiment(flag, result.Reason);
            return new EvaluationEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                Context = context,
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
            Context context,
            LdValue defaultValue,
            EvaluationErrorKind errorKind
            )
        {
            return new EvaluationEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                Context = context,
                FlagKey = flag.Key,
                FlagVersion = flag.Version,
                Value = defaultValue,
                Default = defaultValue,
                Reason = _withReasons ? EvaluationReason.ErrorReason(errorKind) : (EvaluationReason?)null,
                TrackEvents = flag.TrackEvents,
                DebugEventsUntilDate = flag.DebugEventsUntilDate
            };
        }

        internal EvaluationEvent NewUnknownFlagEvaluationEvent(
            string flagKey,
            Context context,
            LdValue defaultValue,
            EvaluationErrorKind errorKind
            )
        {
            return new EvaluationEvent
            {
                Timestamp = UnixMillisecondTime.Now,
                Context = context,
                FlagKey = flagKey,
                Value = defaultValue,
                Default = defaultValue,
                Reason = _withReasons ? EvaluationReason.ErrorReason(errorKind) : (EvaluationReason?)null
            };
        }

        internal EvaluationEvent NewPrerequisiteEvaluationEvent(
            FeatureFlag prereqFlagBeingEvaluated,
            Context context,
            EvaluationDetail<LdValue> result,
            string flagKeyThatReferencesPrerequisite
            )
        {
            var e = NewEvaluationEvent(prereqFlagBeingEvaluated, context, result, LdValue.Null);
            e.PrerequisiteOf = flagKeyThatReferencesPrerequisite;
            return e;
        }

        internal static bool IsExperiment(FeatureFlag flag, EvaluationReason? reason)
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

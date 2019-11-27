using LaunchDarkly.Sdk.Internal.Events;

namespace LaunchDarkly.Sdk.Server.Model
{
    // A simple adapter for use with the CommonSdk event-processing classes which operate
    // on a limited abstraction of the flag properties. Since it's is a value type, there's
    // minimal overhead to creating this wrapper.

    internal struct FeatureFlagEventProperties : IFlagEventProperties
    {
        private readonly FeatureFlag _flag;

        internal FeatureFlagEventProperties(FeatureFlag flag)
        {
            _flag = flag;
        }

        public string Key => _flag.Key;

        public int Version => _flag.Version;

        public int EventVersion => _flag.Version;

        public bool TrackEvents => _flag.TrackEvents;

        public long? DebugEventsUntilDate => _flag.DebugEventsUntilDate;

        // This method is called by EventFactory to determine if extra tracking should be
        // enabled for an event, based on the evaluation reason.
        public bool IsExperiment(EvaluationReason? reason)
        {
            if (!reason.HasValue)
            {
                return false;
            }
            var r = reason.Value;
            switch (r.Kind)
            {
                case EvaluationReasonKind.FALLTHROUGH:
                    return _flag.TrackEventsFallthrough;
                case EvaluationReasonKind.RULE_MATCH:
                    return r.RuleIndex >= 0 && _flag.Rules != null && r.RuleIndex < _flag.Rules.Count &&
                        _flag.Rules[r.RuleIndex].TrackEvents;
            }
            return false;
        }
    }
}

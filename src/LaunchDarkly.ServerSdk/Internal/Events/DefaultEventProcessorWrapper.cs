using LaunchDarkly.Sdk.Server.Interfaces;

using InternalEventProcessor = LaunchDarkly.Sdk.Internal.Events.EventProcessor;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class DefaultEventProcessorWrapper : IEventProcessor
    {
        private readonly InternalEventProcessor _impl;

        internal DefaultEventProcessorWrapper(InternalEventProcessor impl)
        {
            _impl = impl;
        }

        public void RecordEvaluationEvent(EventProcessorTypes.EvaluationEvent e)
        {
            _impl.RecordEvaluationEvent(
                e.Timestamp,
                e.User,
                e.FlagKey,
                e.FlagVersion,
                e.Variation,
                e.Value,
                e.Default,
                e.Reason,
                e.PrerequisiteOf,
                e.TrackEvents,
                e.DebugEventsUntilDate
                );
        }

        public void RecordIdentifyEvent(EventProcessorTypes.IdentifyEvent e)
        {
            _impl.RecordIdentifyEvent(e.Timestamp, e.User);
        }

        public void RecordCustomEvent(EventProcessorTypes.CustomEvent e)
        {
            _impl.RecordCustomEvent(
                e.Timestamp,
                e.User,
                e.EventKey,
                e.Data,
                e.MetricValue
                );
        }

        public void Flush()
        {
            _impl.Flush();
        }

        public void Dispose()
        {
            _impl.Dispose();
        }
    }
}

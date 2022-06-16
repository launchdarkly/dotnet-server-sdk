using LaunchDarkly.Sdk.Server.Subsystems;

using InternalEventProcessor = LaunchDarkly.Sdk.Internal.Events.EventProcessor;
using InternalEventTypes = LaunchDarkly.Sdk.Internal.Events.EventTypes;

namespace LaunchDarkly.Sdk.Server.Internal.Events
{
    internal class DefaultEventProcessorWrapper : IEventProcessor
    {
        private readonly InternalEventProcessor _impl;

        internal DefaultEventProcessorWrapper(InternalEventProcessor impl)
        {
            _impl = impl;
        }

        public void RecordEvaluationEvent(EventProcessorTypes.EvaluationEvent e) =>
            _impl.RecordEvaluationEvent(new InternalEventTypes.EvaluationEvent
            {
                Timestamp = e.Timestamp,
                Context = e.Context,
                FlagKey = e.FlagKey,
                FlagVersion = e.FlagVersion,
                Variation = e.Variation,
                Value = e.Value,
                Default = e.Default,
                Reason = e.Reason,
                PrereqOf = e.PrerequisiteOf,
                TrackEvents = e.TrackEvents,
                DebugEventsUntilDate = e.DebugEventsUntilDate
            });

        public void RecordIdentifyEvent(EventProcessorTypes.IdentifyEvent e) =>
            _impl.RecordIdentifyEvent(new InternalEventTypes.IdentifyEvent
            {
                Timestamp = e.Timestamp,
                Context = e.Context
            });

        public void RecordCustomEvent(EventProcessorTypes.CustomEvent e) =>
            _impl.RecordCustomEvent(new InternalEventTypes.CustomEvent
            {
                Timestamp = e.Timestamp,
                Context = e.Context,
                EventKey = e.EventKey,
                Data = e.Data,
                MetricValue = e.MetricValue
            });

        public void Flush() =>
            _impl.Flush();

        public void Dispose() =>
            _impl.Dispose();
    }
}

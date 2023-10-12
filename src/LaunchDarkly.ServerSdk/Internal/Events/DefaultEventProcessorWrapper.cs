using System;
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
                DebugEventsUntilDate = e.DebugEventsUntilDate,
                SamplingRatio = e.SamplingRatio,
                ExcludeFromSummaries = e.ExcludeFromSummaries
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

        private static InternalEventTypes.MigrationOpEvent ConvertMigrationOpEvent(EventProcessorTypes.MigrationOpEvent inEvent)
        {
            var outEvent = new InternalEventTypes.MigrationOpEvent
            {
                Timestamp = inEvent.Timestamp,
                Context = inEvent.Context,
                Operation = inEvent.Operation,
                SamplingRatio = inEvent.SamplingRatio,
                FlagKey = inEvent.FlagKey,
                FlagVersion = inEvent.FlagVersion,
                Variation = inEvent.Variation,
                Value = inEvent.Value,
                Default = inEvent.Default,
                Reason = inEvent.Reason,
                Invoked = new InternalEventTypes.MigrationOpEvent.InvokedMeasurement
                {
                    Old = inEvent.Invoked.Old,
                    New = inEvent.Invoked.New
                }
            };
            if (inEvent.Latency.HasValue)
            {
                outEvent.Latency = new InternalEventTypes.MigrationOpEvent.LatencyMeasurement
                {
                    Old = inEvent.Latency?.Old,
                    New = inEvent.Latency?.New
                };
            }

            if (inEvent.Error.HasValue)
            {
                outEvent.Error = new InternalEventTypes.MigrationOpEvent.ErrorMeasurement
                {
                    Old = inEvent.Error?.Old ?? false,
                    New = inEvent.Error?.New ?? false
                };
            }

            if (inEvent.Consistent.HasValue)
            {
                outEvent.Consistent = new InternalEventTypes.MigrationOpEvent.ConsistentMeasurement
                {
                    IsConsistent = inEvent.Consistent?.IsConsistent ?? false,
                    SamplingRatio = inEvent.Consistent?.SamplingRatio ?? 1
                };
            }
            return outEvent;
        }

        public void RecordMigrationEvent(EventProcessorTypes.MigrationOpEvent e) =>
            _impl.RecordMigrationOpEvent(ConvertMigrationOpEvent(e));

        public void Flush() =>
            _impl.Flush();

        public bool FlushAndWait(TimeSpan timeout) =>
            _impl.FlushAndWait(timeout);

        public void Dispose() =>
            _impl.Dispose();
    }
}

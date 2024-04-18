
namespace LaunchDarkly.Sdk.Server.Subsystems
{
    /// <summary>
    /// Parameter types for use by <see cref="IEventProcessor"/> implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Application code normally does not need to use these types or interact directly with any
    /// <see cref="IEventProcessor"/> functionality. They are provided to allow a custom implementation
    /// or test fixture to be substituted for the SDK's normal analytics event logic.
    /// </para>
    /// <para>
    /// These types deliberately duplicate the corresponding parameter types that are defined in
    /// <c>LaunchDarkly.InternalSdk</c>. The point of this duplication is to keep all symbols from
    /// <c>LaunchDarkly.InternalSdk</c> out of the public SDK API so that application code will
    /// never depend on the implementation details in that assembly, which is versioned separately
    /// from the SDK and may change in backward-incompatible ways.
    /// </para>
    /// </remarks>
    public static class EventProcessorTypes
    {
        /// <summary>
        /// Parameters for <see cref="IEventProcessor.RecordEvaluationEvent(EvaluationEvent)"/>.
        /// </summary>
        public struct EvaluationEvent
        {
            /// <summary>
            /// Date/timestamp of the event.
            /// </summary>
            public UnixMillisecondTime Timestamp { get; set; }

            /// <summary>
            /// The evaluation context for the event. Some attributes may not be sent
            /// to LaunchDarkly if they are private.
            /// </summary>
            public Context Context { get; set; }

            /// <summary>
            /// The unique key of the feature flag involved in the event.
            /// </summary>
            public string FlagKey { get; set; }

            /// <summary>
            /// The version of the flag.
            /// </summary>
            public int? FlagVersion { get; set; }

            /// <summary>
            /// The variation index for the computed value of the flag.
            /// </summary>
            public int? Variation { get; set; }

            /// <summary>
            /// The computed value of the flag.
            /// </summary>
            public LdValue Value { get; set; }

            /// <summary>
            /// The default value of the flag.
            /// </summary>
            public LdValue Default { get; set; }

            /// <summary>
            /// An explanation of how the value was calculated, or null if the reason was not requested.
            /// </summary>
            public EvaluationReason? Reason { get; set; }

            /// <summary>
            /// The key of the flag that this flag is a prerequisite of, if any.
            /// </summary>
            public string PrerequisiteOf { get; set; }

            /// <summary>
            /// True if full-fidelity analytics events should be sent for this flag.
            /// </summary>
            public bool TrackEvents { get; set; }

            /// <summary>
            /// If set, debug events are being generated until this date/time.
            /// </summary>
            public UnixMillisecondTime? DebugEventsUntilDate { get; set; }

            /// <summary>
            /// Sampling ratio which determines is used for sampling the event.
            /// </summary>
            public long? SamplingRatio { get; set; }

            /// <summary>
            /// If true the event will not be included in summaries.
            /// </summary>
            public bool ExcludeFromSummaries { get; set; }
        }

        /// <summary>
        /// Parameters for <see cref="IEventProcessor.RecordIdentifyEvent(IdentifyEvent)"/>.
        /// </summary>
        public struct IdentifyEvent
        {
            /// <summary>
            /// Date/timestamp of the event.
            /// </summary>
            public UnixMillisecondTime Timestamp { get; set; }

            /// <summary>
            /// The evaluation context. Some attributes may not be sent to LaunchDarkly if they are private.
            /// </summary>
            public Context Context { get; set; }
        }

        /// <summary>
        /// Parameters for <see cref="IEventProcessor.RecordCustomEvent(CustomEvent)"/>.
        /// </summary>
        public struct CustomEvent
        {
            /// <summary>
            /// Date/timestamp of the event.
            /// </summary>
            public UnixMillisecondTime Timestamp { get; set; }

            /// <summary>
            /// The evaluation context. Some attributes may not be sent to LaunchDarkly if they are private.
            /// </summary>
            public Context Context { get; set; }

            /// <summary>
            /// The event key.
            /// </summary>
            public string EventKey { get; set; }


            /// <summary>
            /// Custom data provided for the event.
            /// </summary>
            public LdValue Data { get; set; }

            /// <summary>
            /// An optional numeric value that can be used in analytics.
            /// </summary>
            public double? MetricValue { get; set; }
        }

        /// <summary>
        /// Parameters for <see cref="IEventProcessor.RecordMigrationEvent(EventProcessorTypes.MigrationOpEvent)"/>
        /// </summary>
        public struct MigrationOpEvent
        {
            #region Measurement Types

            /// <summary>
            /// Information about what origins were invoked.
            /// </summary>
            /// <remarks>At lest one measurement must be invoked.</remarks>
            public struct InvokedMeasurement
            {
                /// <summary>
                /// True if the old method was invoked.
                /// </summary>
                public bool Old { get; set; }

                /// <summary>
                /// True if the new method was invoked.
                /// </summary>
                public bool New { get; set; }
            }

            /// <summary>
            /// Latency measurements for invoked methods.
            /// </summary>
            /// <remarks>A method must be invoked for there to be any measurements associated with it.</remarks>
            public struct LatencyMeasurement
            {
                /// <summary>
                /// Latency of the old method, or null if the old method was not invoked.
                /// </summary>
                public long? Old { get; set; }

                /// <summary>
                /// Latency of the new method, or null if the new method was not invoked.
                /// </summary>
                public long? New { get; set; }
            }

            /// <summary>
            /// Error measurements for invoked methods.
            /// </summary>
            /// <remarks>A method must be invoked for there to be any measurements associated with it.</remarks>
            public struct ErrorMeasurement
            {
                /// <summary>
                /// True if any error happened for the old method.
                /// </summary>
                public bool Old { get; set; }

                /// <summary>
                /// True if any error happened for the new method.
                /// </summary>
                public bool New { get; set; }
            }

            /// <summary>
            /// Consistency measurement.
            /// </summary>
            /// <remarks>Both measurements MUST be invoked if a consistency measurement is included.</remarks>
            public struct ConsistentMeasurement
            {
                /// <summary>
                /// True if the measurement was consistent.
                /// </summary>
                public bool IsConsistent { get; set; }

                /// <summary>
                /// The sampling ratio for the consistency check.
                /// </summary>
                public long SamplingRatio { get; set; }
            }

            #endregion

            /// <summary>
            /// Date/timestamp of the event.
            /// </summary>
            public UnixMillisecondTime Timestamp { get; set; }

            /// <summary>
            /// The evaluation context. Some attributes may not be sent to LaunchDarkly if they are private.
            /// </summary>
            public Context Context { get; set; }

            /// <summary>
            /// The type of migration operation that was performed (read/write).
            /// </summary>
            public string Operation { get; set; }

            /// <summary>
            /// The sampling ratio for this event.
            /// </summary>
            public long SamplingRatio { get; set; }

            #region Evaluation Detail

            /// <summary>
            /// The flag key for the migration.
            /// </summary>
            public string FlagKey { get; set; }

            /// <summary>
            /// The version of the flag.
            /// </summary>
            public int? FlagVersion { get; set; }

            /// <summary>
            /// The variation for the evaluation.
            /// </summary>
            public int? Variation { get; set; }

            /// <summary>
            /// The value of the evaluation.
            /// </summary>
            public LdValue Value { get; set; }

            /// <summary>
            /// The default value of the evaluation.
            /// </summary>
            public LdValue Default { get; set; }

            /// <summary>
            /// The reason associated with the evaluation.
            /// </summary>
            public EvaluationReason? Reason { get; set; }

            #endregion

            #region Measurements

            /// <summary>
            /// The invoked measurement.
            /// </summary>
            public InvokedMeasurement Invoked { get; set; }

            /// <summary>
            /// The latency measurement.
            /// </summary>
            /// <remarks>
            /// Should not be included if no latency measurements were taken.
            /// </remarks>
            public LatencyMeasurement? Latency { get; set; }

            /// <summary>
            /// The error measurement.
            /// </summary>
            /// <remarks>
            /// Should not be included if there were no errors during the migration.
            /// </remarks>
            public ErrorMeasurement? Error { get; set; }

            /// <summary>
            /// The consistency measurement.
            /// </summary>
            /// <remarks>
            /// Should not be included if a consistency check was not performed. There are effectively 3 states,
            /// a consistency check was not performed, it was performed and it was inconsistent, it was performed and
            /// it was inconsistent. It is important to track these cases correctly for accurate analytics.
            /// </remarks>
            public ConsistentMeasurement? Consistent { get; set; }

            #endregion
        }
    }
}

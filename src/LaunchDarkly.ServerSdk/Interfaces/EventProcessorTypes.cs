
namespace LaunchDarkly.Sdk.Server.Interfaces
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
            /// Attributes of the user who generated the event. Some attributes may not be sent
            /// to LaunchDarkly if they are private.
            /// </summary>
            public User User { get; set; }

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
            /// Attributes of the user being identified. Some attributes may not be sent
            /// to LaunchDarkly if they are private.
            /// </summary>
            public User User { get; set; }
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
            /// Attributes of the user who generated the event. Some attributes may not be sent
            /// to LaunchDarkly if they are private.
            /// </summary>
            public User User { get; set; }

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
        /// Parameters for <see cref="IEventProcessor.RecordAliasEvent(AliasEvent)"/>.
        /// </summary>
        public struct AliasEvent
        {
            /// <summary>
            /// Date/timestamp of the event.
            /// </summary>
            public UnixMillisecondTime Timestamp { get; set; }

            /// <summary>
            /// Key of the new user.
            /// </summary>
            public string CurrentKey { get; set; }

            /// <summary>
            /// Type of the new user.
            /// </summary>
            public ContextKind CurrentKind { get; set; }

            /// <summary>
            /// Key of the previous user.
            /// </summary>
            public string PreviousKey { get; set; }

            /// <summary>
            /// Type of the previous user.
            /// </summary>
            public ContextKind PreviousKind { get; set; }
        }

        /// <summary>
        /// Used with <see cref="AliasEvent"/> to indicate the category of a key.
        /// </summary>
        public enum ContextKind
        {
            /// <summary>
            /// The key belongs to a non-anonymous user.
            /// </summary>
            User,

            /// <summary>
            /// The key belongs to an anonymous user.
            /// </summary>
            AnonymousUser
        }
    }
}

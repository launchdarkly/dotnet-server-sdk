using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.Events;

using static LaunchDarkly.Sdk.Internal.Events.DiagnosticConfigProperties;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Contains methods for configuring delivery of analytics events.
    /// </summary>
    /// <remarks>
    /// The SDK normally buffers analytics events and sends them to LaunchDarkly at intervals. If you want
    /// to customize this behavior, create a builder with <see cref="Components.SendEvents"/>, change its
    /// properties with the methods of this class, and pass it to <see cref="ConfigurationBuilder.Events"/>.
    /// </remarks>
    /// <example>
    /// <code>
    ///     var config = Configuration.Builder(sdkKey)
    ///         .Events(
    ///             Components.SendEvents().Capacity(5000).FlushInterval(TimeSpan.FromSeconds(2))
    ///         )
    ///         .Build();
    /// </code>
    /// </example>
    public sealed class EventProcessorBuilder : IComponentConfiguration<IEventProcessor>, IDiagnosticDescription
    {
        /// <summary>
        /// The default value for <see cref="Capacity(int)"/>.
        /// </summary>
        public const int DefaultCapacity = 10000;

        /// <summary>
        /// The default value for <see cref="DiagnosticRecordingInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultDiagnosticRecordingInterval = TimeSpan.FromMinutes(15);

        /// <summary>
        /// The default value for <see cref="FlushInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The default value for <see cref="ContextKeysCapacity(int)"/>.
        /// </summary>
        public const int DefaultContextKeysCapacity = 1000;

        /// <summary>
        /// The default value for <see cref="ContextKeysFlushInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultContextKeysFlushInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The minimum value for <see cref="DiagnosticRecordingInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan MinimumDiagnosticRecordingInterval = TimeSpan.FromMinutes(1);

        internal bool _allAttributesPrivate = false;
        internal int _capacity = DefaultCapacity;
        internal TimeSpan _diagnosticRecordingInterval = DefaultDiagnosticRecordingInterval;
        internal TimeSpan _flushInterval = DefaultFlushInterval;
        internal HashSet<AttributeRef> _privateAttributes = new HashSet<AttributeRef>();
        internal int _contextKeysCapacity = DefaultContextKeysCapacity;
        internal TimeSpan _contextKeysFlushInterval = DefaultContextKeysFlushInterval;
        internal IEventSender _eventSender = null; // used in testing

        /// <summary>
        /// Sets whether or not all optional context attributes should be hidden from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// If this is <see langword="true"/>, all contextattribute values (other than the key) will be private, not just
        /// the attributes specified in <see cref="PrivateAttributes(string[])"/> or on a per-context basis with
        /// <see cref="ContextBuilder"/> methods. By default, it is <see langword="false"/>.
        /// </remarks>
        /// <param name="allAttributesPrivate">true if all context attributes should be private</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder AllAttributesPrivate(bool allAttributesPrivate)
        {
            _allAttributesPrivate = allAttributesPrivate;
            return this;
        }

        /// <summary>
        /// Sets the capacity of the events buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The client buffers up to this many events in memory before flushing. If the capacity is exceeded before
        /// the buffer is flushed (see <see cref="FlushInterval(TimeSpan)"/>), events will be discarded. Increasing the
        /// capacity means that events are less likely to be discarded, at the cost of consuming more memory.
        /// </para>
        /// <para>
        /// The default value is <see cref="DefaultCapacity"/>. A zero or negative value will be changed to the default.
        /// </para>
        /// </remarks>
        /// <param name="capacity">the capacity of the event buffer</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder Capacity(int capacity)
        {
            _capacity = (capacity <= 0) ? DefaultCapacity : capacity;
            return this;
        }

        /// <summary>
        /// Sets the interval at which periodic diagnostic data is sent.
        /// </summary>
        /// <remarks>
        /// The default value is <see cref="DefaultDiagnosticRecordingInterval"/>; the minimum value is
        /// <see cref="MinimumDiagnosticRecordingInterval"/>. This property is ignored if
        /// <see cref="ConfigurationBuilder.DiagnosticOptOut(bool)"/> is set to <see langword="true"/>.
        /// </remarks>
        /// <param name="diagnosticRecordingInterval">the diagnostics interval</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder DiagnosticRecordingInterval(TimeSpan diagnosticRecordingInterval)
        {
            _diagnosticRecordingInterval =
                diagnosticRecordingInterval.CompareTo(MinimumDiagnosticRecordingInterval) < 0 ?
                MinimumDiagnosticRecordingInterval : diagnosticRecordingInterval;
            return this;
        }

        // Used only in testing
        internal EventProcessorBuilder DiagnosticRecordingIntervalNoMinimum(TimeSpan diagnosticRecordingInterval)
        {
            _diagnosticRecordingInterval = diagnosticRecordingInterval;
            return this;
        }

        // Used only in testing
        internal EventProcessorBuilder EventSender(IEventSender eventSender)
        {
            _eventSender = eventSender;
            return this;
        }

        /// <summary>
        /// Sets the interval between flushes of the event buffer.
        /// </summary>
        /// <remarks>
        /// Decreasing the flush interval means that the event buffer is less likely to reach capacity.
        /// The default value is <see cref="DefaultFlushInterval"/>. A zero or negative value will be changed to
        /// the default.
        /// </remarks>
        /// <param name="flushInterval">the flush interval</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder FlushInterval(TimeSpan flushInterval)
        {
            _flushInterval = (flushInterval.CompareTo(TimeSpan.Zero) <= 0) ?
                DefaultFlushInterval : flushInterval;
            return this;
        }

        /// <summary>
        /// Marks a set of attribute names or subproperties as private.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any contexts sent to LaunchDarkly with this configuration active will have attributes with these
        /// names removed. This is in addition to any attributes that were marked as private for an
        /// individual context with <see cref="ContextBuilder"/> methods.
        /// </para>
        /// <para>
        /// If and only if a parameter starts with a slash, it is interpreted as a slash-delimited path that
        /// can denote a nested property within a JSON object. For instance, "/address/street" means that if
        /// there is an attribute called "address" that is a JSON object, and one of the object's properties
        /// is "street", the "street" property will be redacted from the analytics data but other properties
        /// within "address" will still be sent. This syntax also uses the JSON Pointer convention of escaping
        /// a literal slash character as "~1" and a tilde as "~0".
        /// </para>
        /// <para>
        /// This method replaces any previous <see cref="PrivateAttributes(string[])"/> that were set on the
        /// same builder, rather than adding to them.
        /// </para>
        /// </remarks>
        /// <param name="attributes">a set of names or paths that will be removed from context data sent to
        /// LaunchDarkly</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder PrivateAttributes(params string[] attributes)
        {
            _privateAttributes.Clear();
            foreach (var a in attributes)
            {
                _privateAttributes.Add(AttributeRef.FromPath(a));
            }
            return this;
        }

        /// <summary>
        /// Sets the number of context keys that the event processor can remember at any one time.
        /// </summary>
        /// <remarks>
        /// To avoid sending duplicate context details in analytics events, the SDK maintains a cache of
        /// recently seen contexts, expiring at an interval set by <see cref="ContextKeysFlushInterval(TimeSpan)"/>.
        /// The default value for the size of this cache is <see cref="DefaultContextKeysCapacity"/>. A zero or
        /// negative value will be changed to the default.
        /// </remarks>
        /// <param name="contextKeysCapacity">the maximum number of context keys to remember</param>
        /// <returns>the builder</returns>
        /// <seealso cref="ContextKeysFlushInterval(TimeSpan)"/>
        public EventProcessorBuilder ContextKeysCapacity(int contextKeysCapacity)
        {
            _contextKeysCapacity = (contextKeysCapacity <= 0) ? DefaultContextKeysCapacity : contextKeysCapacity;
            return this;
        }

        /// <summary>
        /// Sets the interval at which the event processor will reset its cache of known context keys.
        /// </summary>
        /// <remarks>
        /// The default value is <see cref="DefaultContextKeysFlushInterval"/>. A zero or negative value will be
        /// changed to the default.
        /// </remarks>
        /// <param name="contextKeysFlushInterval">the flush interval</param>
        /// <returns>the builder</returns>
        /// <see cref="ContextKeysCapacity(int)"/>
        public EventProcessorBuilder ContextKeysFlushInterval(TimeSpan contextKeysFlushInterval)
        {
            _contextKeysFlushInterval = (contextKeysFlushInterval.CompareTo(TimeSpan.Zero) <= 0) ?
                DefaultContextKeysFlushInterval : contextKeysFlushInterval;
            return this;
        }

        /// <inheritdoc/>
        public IEventProcessor Build(LdClientContext context)
        {
            var eventsConfig = MakeEventsConfiguration(context, true);
            var logger = context.Logger.SubLogger(LogNames.EventsSubLog);
            var eventSender = _eventSender ??
                new DefaultEventSender(
                    context.Http.HttpProperties,
                    eventsConfig,
                    logger
                    );
            return new DefaultEventProcessorWrapper(
                new EventProcessor(
                    eventsConfig,
                    eventSender,
                    new DefaultContextDeduplicator(_contextKeysCapacity, _contextKeysFlushInterval),
                    context.DiagnosticStore,
                    null,
                    logger,
                    null
                    ));
        }

        private EventsConfiguration MakeEventsConfiguration(LdClientContext context, bool logConfigErrors)
        {
            var configuredBaseUri = StandardEndpoints.SelectBaseUri(
                context.ServiceEndpoints, e => e.EventsBaseUri, "Events",
                    logConfigErrors ? context.Logger : Logs.None.Logger(""));
            return new EventsConfiguration
            {
                AllAttributesPrivate = _allAttributesPrivate,
                DiagnosticRecordingInterval = _diagnosticRecordingInterval,
                EventCapacity = _capacity,
                EventFlushInterval = _flushInterval,
                EventsUri = configuredBaseUri.AddPath("bulk"),
                DiagnosticUri = configuredBaseUri.AddPath("diagnostic"),
                PrivateAttributes = _privateAttributes.ToImmutableHashSet()
            };
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(LdClientContext context) =>
            LdValue.BuildObject()
                .WithEventProperties(
                    MakeEventsConfiguration(context, false),
                    StandardEndpoints.IsCustomUri(context.ServiceEndpoints, e => e.EventsBaseUri)
                )
                .Add("userKeysCapacity", _contextKeysCapacity) // these two properties are specific to the server-side SDK
                .Add("userKeysFlushIntervalMillis", _contextKeysFlushInterval.TotalMilliseconds)
                .Build();
    }
}

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
    /// properties with the methods of this class, and pass it to <see cref="ConfigurationBuilder.Events(IEventProcessorFactory)"/>.
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
    public sealed class EventProcessorBuilder : IEventProcessorFactory, IDiagnosticDescription
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
        /// The default value for <see cref="UserKeysCapacity(int)"/>.
        /// </summary>
        public const int DefaultUserKeysCapacity = 1000;

        /// <summary>
        /// The default value for <see cref="UserKeysFlushInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultUserKeysFlushInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The minimum value for <see cref="DiagnosticRecordingInterval(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan MinimumDiagnosticRecordingInterval = TimeSpan.FromMinutes(1);

        internal bool _allAttributesPrivate = false;
        internal Uri _baseUri = null;
        internal int _capacity = DefaultCapacity;
        internal TimeSpan _diagnosticRecordingInterval = DefaultDiagnosticRecordingInterval;
        internal TimeSpan _flushInterval = DefaultFlushInterval;
        internal bool _inlineUsersInEvents = false;
        internal HashSet<UserAttribute> _privateAttributes = new HashSet<UserAttribute>();
        internal int _userKeysCapacity = DefaultUserKeysCapacity;
        internal TimeSpan _userKeysFlushInterval = DefaultUserKeysFlushInterval;
        internal IEventSender _eventSender = null; // used in testing

        /// <summary>
        /// Sets whether or not all optional user attributes should be hidden from LaunchDarkly.
        /// </summary>
        /// <remarks>
        /// If this is <see langword="true"/>, all user attribute values (other than the key) will be private, not just
        /// the attributes specified in <see cref="PrivateAttributes(UserAttribute[])"/> or on a per-user basis with
        /// <see cref="UserBuilder"/> methods. By default, it is <see langword="false"/>.
        /// </remarks>
        /// <param name="allAttributesPrivate">true if all user attributes should be private</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder AllAttributesPrivate(bool allAttributesPrivate)
        {
            _allAttributesPrivate = allAttributesPrivate;
            return this;
        }

        /// <summary>
        /// Deprecated method for setting a custom base URI for the events service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The preferred way to set this option is now with
        /// <see cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)"/>. If you set
        /// this deprecated option, it overrides any value that was set with
        /// <see cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)"/>.
        /// </para>
        /// <para>
        /// You will only need to change this value in the following cases:
        /// </para>
        /// <list type="bullet">
        /// <item><description>
        /// You are using the <a href="https://docs.launchdarkly.com/home/relay-proxy">Relay Proxy</a>.
        /// Set <c>BaseUri</c> to the base URI of the Relay Proxy instance.
        /// </description></item>
        /// <item><description>
        /// You are connecting to a test server or a nonstandard endpoint for the LaunchDarkly service.
        /// </description></item>
        /// </list>
        /// </remarks>
        /// <param name="baseUri">the base URI of the events service; null to use the default</param>
        /// <returns>the builder</returns>
        /// <seealso cref="ConfigurationBuilder.ServiceEndpoints(ServiceEndpointsBuilder)"/>
        [Obsolete("Use ConfigurationBuilder.ServiceEndpoints instead")]
        public EventProcessorBuilder BaseUri(Uri baseUri)
        {
            _baseUri = baseUri;
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
        /// Sets whether to include full user details in every analytics event.
        /// </summary>
        /// <remarks>
        /// The default value is <see langword="false"/>: events will only include the user key, except for one
        /// "index" event that provides the full details for the user.
        /// </remarks>
        /// <param name="inlineUsersInEvents">true if you want full user details in each event</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder InlineUsersInEvents(bool inlineUsersInEvents)
        {
            _inlineUsersInEvents = inlineUsersInEvents;
            return this;
        }

        /// <summary>
        /// Marks a set of attribute names as private.
        /// </summary>
        /// <remarks>
        /// Any users sent to LaunchDarkly with this configuration active will have attributes with these
        /// names removed. This is in addition to any attributes that were marked as private for an
        /// individual user with <see cref="UserBuilder"/> methods.
        /// </remarks>
        /// <param name="attributes">a set of attributes that will be removed from user data set to LaunchDarkly</param>
        /// <returns>the builder</returns>
        /// <seealso cref="PrivateAttributeNames(string[])"/>
        public EventProcessorBuilder PrivateAttributes(params UserAttribute[] attributes)
        {
            foreach (var a in attributes)
            {
                _privateAttributes.Add(a);
            }
            return this;
        }

        /// <summary>
        /// Marks a set of attribute names as private.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any users sent to LaunchDarkly with this configuration active will have attributes with these
        /// names removed. This is in addition to any attributes that were marked as private for an
        /// individual user with <see cref="UserBuilder"/> methods.
        /// </para>
        /// <para>
        /// Using <see cref="PrivateAttributes(UserAttribute[])"/> is preferable to avoid the possibility of
        /// misspelling a built-in attribute.
        /// </para>
        /// </remarks>
        /// <param name="attributes">a set of names that will be removed from user data set to LaunchDarkly</param>
        /// <returns>the builder</returns>
        /// <seealso cref="PrivateAttributes(UserAttribute[])"/>
        public EventProcessorBuilder PrivateAttributeNames(params string[] attributes)
        {
            foreach (var a in attributes)
            {
                _privateAttributes.Add(UserAttribute.ForName(a));
            }
            return this;
        }

        /// <summary>
        /// Sets the number of user keys that the event processor can remember at any one time.
        /// </summary>
        /// <remarks>
        /// To avoid sending duplicate user details in analytics events, the SDK maintains a cache of
        /// recently seen user keys, expiring at an interval set by <see cref="UserKeysFlushInterval(TimeSpan)"/>.
        /// The default value for the size of this cache is <see cref="DefaultUserKeysCapacity"/>. A zero or
        /// negative value will be changed to the default.
        /// </remarks>
        /// <param name="userKeysCapacity">the maximum number of user keys to remember</param>
        /// <returns>the builder</returns>
        public EventProcessorBuilder UserKeysCapacity(int userKeysCapacity)
        {
            _userKeysCapacity = (userKeysCapacity <= 0) ? DefaultUserKeysCapacity : userKeysCapacity;
            return this;
        }

        /// <summary>
        /// Sets the interval at which the event processor will reset its cache of known user keys.
        /// </summary>
        /// <remarks>
        /// The default value is <see cref="DefaultUserKeysFlushInterval"/>. A zero or negative value will be
        /// changed to the default.
        /// </remarks>
        /// <param name="userKeysFlushInterval">the flush interval</param>
        /// <returns>the builder</returns>
        /// <see cref="UserKeysCapacity(int)"/>
        public EventProcessorBuilder UserKeysFlushInterval(TimeSpan userKeysFlushInterval)
        {
            _userKeysFlushInterval = (userKeysFlushInterval.CompareTo(TimeSpan.Zero) <= 0) ?
                DefaultUserKeysFlushInterval : userKeysFlushInterval;
            return this;
        }

        /// <inheritdoc/>
        public IEventProcessor CreateEventProcessor(LdClientContext context)
        {
            var eventsConfig = MakeEventsConfiguration(context.Basic, true);
            var logger = context.Basic.Logger.SubLogger(LogNames.EventsSubLog);
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
                    new DefaultUserDeduplicator(_userKeysCapacity, _userKeysFlushInterval),
                    context.DiagnosticStore,
                    null,
                    logger,
                    null
                    ));
        }

        private EventsConfiguration MakeEventsConfiguration(BasicConfiguration basic, bool logConfigErrors)
        {
            var configuredBaseUri = _baseUri ??
                StandardEndpoints.SelectBaseUri(basic.ServiceEndpoints, e => e.EventsBaseUri, "Events",
                    logConfigErrors ? basic.Logger : Logs.None.Logger(""));
            return new EventsConfiguration
            {
                AllAttributesPrivate = _allAttributesPrivate,
                DiagnosticRecordingInterval = _diagnosticRecordingInterval,
                EventCapacity = _capacity,
                EventFlushInterval = _flushInterval,
                EventsUri = configuredBaseUri.AddPath("bulk"),
                DiagnosticUri = configuredBaseUri.AddPath("diagnostic"),
                InlineUsersInEvents = _inlineUsersInEvents,
                PrivateAttributeNames = _privateAttributes.ToImmutableHashSet(),
                UserKeysCapacity = _userKeysCapacity,
                UserKeysFlushInterval = _userKeysFlushInterval
            };
        }

        /// <inheritdoc/>
        public LdValue DescribeConfiguration(BasicConfiguration basic) =>
            LdValue.BuildObject()
                .WithEventProperties(
                    MakeEventsConfiguration(basic, false),
                    StandardEndpoints.IsCustomUri(basic.ServiceEndpoints, _baseUri, e => e.EventsBaseUri)
                )
                .Add("userKeysCapacity", _userKeysCapacity) // these two properties are specific to the server-side SDK
                .Add("userKeysFlushIntervalMillis", _userKeysFlushInterval.TotalMilliseconds)
                .Build();
    }
}

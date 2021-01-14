using System;
using System.Collections.Generic;
using System.Net.Http;
using Common.Logging;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Extension methods that can be called on a <see cref="Configuration"/> to add to its properties.
    /// It is preferable to use <see cref="Configuration.Builder(string)"/> instead.
    /// </summary>
    [Obsolete(ObsoleteMessage)]
    public static class ConfigurationExtensions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConfigurationExtensions));

        private const string ObsoleteMessage =
            "Use Configuration.Builder() and the ConfigurationBuilder methods instead; ConfigurationExtensions modifies Configuration properties, which will eventually become immutable";

        /// <summary>
        /// Sets the base URI of the LaunchDarkly server for this configuration.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="uri">the base URI as a string</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithUri(this Configuration configuration, string uri)
        {
            if (uri != null)
                configuration.BaseUri = new Uri(uri);

            return configuration;
        }

        /// <summary>
        /// Sets the base URI of the LaunchDarkly server for this configuration.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="uri">the base URI</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithUri(this Configuration configuration, Uri uri)
        {
            if (uri != null)
                configuration.BaseUri = uri;

            return configuration;
        }

        /// <summary>
        /// Sets the base URL of the LaunchDarkly streaming server for this configuration.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="uri">the stream URI as a string</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithStreamUri(this Configuration configuration, string uri)
        {
            if (uri != null)
                configuration.StreamUri = new Uri(uri);

            return configuration;
        }

        /// <summary>
        /// Sets the base URL of the LaunchDarkly streaming server for this configuration.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="uri">the stream URI</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithStreamUri(this Configuration configuration, Uri uri)
        {
            if (uri != null)
                configuration.StreamUri = uri;

            return configuration;
        }

        /// <summary>
        /// Sets the base URL of the LaunchDarkly analytics event server for this configuration.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="uri">the events URI as a string</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithEventsUri(this Configuration configuration, string uri)
        {
            if (uri != null)
                configuration.EventsUri = new Uri(uri);

            return configuration;
        }

        /// <summary>
        /// Sets the base URL of the LaunchDarkly analytics event server for this configuration.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="uri">the events URI</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithEventsUri(this Configuration configuration, Uri uri)
        {
            if (uri != null)
                configuration.EventsUri = uri;

            return configuration;
        }

        /// <summary>
        /// Sets the capacity of the events buffer. The client buffers up to this many events in
        /// memory before flushing. If the capacity is exceeded before the buffer is flushed,
        /// events will be discarded. Increasing the capacity means that events are less likely
        /// to be discarded, at the cost of consuming more memory.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="eventQueueCapacity"></param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithEventQueueCapacity(this Configuration configuration, int eventQueueCapacity)
        {
            configuration.EventCapacity = eventQueueCapacity;
            return configuration;
        }

        /// <summary>
        /// Sets the time between flushes of the event buffer. Decreasing the flush interval means
        /// that the event buffer is less likely to reach capacity. The default value is 5 seconds.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="frequency">the flush interval</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithEventQueueFrequency(this Configuration configuration, TimeSpan frequency)
        {
            configuration.EventFlushInterval = frequency;
            return configuration;
        }

        /// <summary>
        /// Enables event sampling if non-zero. When set to the default of zero, all analytics events are
        /// sent back to LaunchDarkly. When greater than zero, there is a 1 in <c>EventSamplingInterval</c>
        /// chance that events will be sent (example: if the interval is 20, on average 5% of events will be sent).
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="interval">the sampling interval</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete("The SamplingInterval feature will be removed in a future version of the SDK")]
        public static Configuration WithEventSamplingInterval(this Configuration configuration, int interval)
        {
            if (interval < 0)
            {
                Log.Warn("EventSamplingInterval cannot be less than zero.");
                interval = 0;
            }
            configuration.EventSamplingInterval = interval;
            return configuration;
        }

        /// <summary>
        /// Sets the polling interval (when streaming is disabled). Values less than the default of
        /// 30 seconds will be changed to the default.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="pollingInterval">the rule update polling interval</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithPollingInterval(this Configuration configuration, TimeSpan pollingInterval)
        {
            if (pollingInterval.CompareTo(Configuration.DefaultPollingInterval) < 0)
            {
                Log.Warn("PollingInterval cannot be less than the default of 30 seconds.");
                pollingInterval = Configuration.DefaultPollingInterval;
            }
            configuration.PollingInterval = pollingInterval;
            return configuration;
        }

        /// <summary>
        /// Sets how long the client constructor will block awaiting a successful connection to
        /// LaunchDarkly. Setting this to 0 will not block and will cause the constructor to return
        /// immediately. The default value is 5 seconds.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="startWaitTime">the length of time to wait</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithStartWaitTime(this Configuration configuration, TimeSpan startWaitTime)
        {
            configuration.StartWaitTime = startWaitTime;
            return configuration;
        }

        /// <summary>
        /// Sets whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="offline">true if the client should remain offline</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithOffline(this Configuration configuration, bool offline)
        {
            configuration.Offline = offline;
            return configuration;
        }

        /// <summary>
        /// Sets the connection timeout. The default value is 10 seconds.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="timeSpan">the connection timeout</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithHttpClientTimeout(this Configuration configuration, TimeSpan timeSpan)
        {
            configuration.HttpClientTimeout = timeSpan;
            return configuration;
        }

        /// <summary>
        /// Sets the timeout when reading data from the EventSource API. The default value is 5 minutes.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="timeSpan">the read timeout</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithReadTimeout(this Configuration configuration, TimeSpan timeSpan)
        {
            configuration.ReadTimeout = timeSpan;
            return configuration;
        }

        /// <summary>
        /// Sets the reconnect base time for the streaming connection. The streaming connection
        /// uses an exponential backoff algorithm (with jitter) for reconnects, but will start the
        /// backoff with a value near the value specified here. The default value is 1 second.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="timeSpan">the reconnect time base value</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithReconnectTime(this Configuration configuration, TimeSpan timeSpan)
        {
            configuration.ReconnectTime = timeSpan;
            return configuration;
        }

        /// <summary>
        /// Sets whether this client should use the <a href="https://docs.launchdarkly.com/docs/the-relay-proxy">LaunchDarkly
        /// relay</a> in daemon mode, instead of subscribing to the streaming or polling API.
        /// For this to work, you must also be using a
        /// <a href="https://docs.launchdarkly.com/docs/using-a-persistent-feature-store">persistent feature store</a>.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="useLdd">true to use the relay in daemon mode; false to use streaming or polling</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithUseLdd(this Configuration configuration, bool useLdd)
        {
            configuration.UseLdd = useLdd;
            return configuration;
        }

        /// <summary>
        /// Obsolete; please use <see cref="WithFeatureStoreFactory"/> instead.
        /// </summary>
        [Obsolete("Deprecated, please use WithFeatureStoreFactory instead.")]
        public static Configuration WithFeatureStore(this Configuration configuration, IFeatureStore featureStore)
        {
            if (featureStore != null)
            {
                configuration.FeatureStore = featureStore;
            }
            return configuration;
        }

        /// <summary>
        /// Sets the implementation of <see cref="IFeatureStore"/> to be used for holding feature flags
        /// and related data received from LaunchDarkly, using a factory object. The default is
        /// <see cref="Components.InMemoryFeatureStore"/>, but you may choose to use a custom implementation.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="factory">the factory object</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithFeatureStoreFactory(this Configuration configuration, IFeatureStoreFactory factory)
        {
            configuration.FeatureStoreFactory = factory;
            return configuration;
        }

        /// <summary>
        /// Sets the implementation of <see cref="IEventProcessor"/> to be used for processing analytics events,
        /// using a factory object. The default is <see cref="Components.DefaultEventProcessor"/>, but
        /// you may choose to use a custom implementation (for instance, a test fixture).
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="factory">the factory object</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithEventProcessorFactory(this Configuration configuration, IEventProcessorFactory factory)
        {
            configuration.EventProcessorFactory = factory;
            return configuration;
        }

        /// <summary>
        /// Sets the implementation of <see cref="IUpdateProcessor"/> to be used for receiving feature flag data,
        /// using a factory object. The default is <see cref="Components.DefaultUpdateProcessor"/>, but
        /// you may choose to use a custom implementation (for instance, a test fixture).
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="factory">the factory object</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithUpdateProcessorFactory(this Configuration configuration, IUpdateProcessorFactory factory)
        {
            configuration.DataSource = factory;
            return configuration;
        }

        /// <summary>
        /// Sets the object to be used for sending HTTP requests. This is exposed for testing purposes.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="httpClientHandler">the <c>HttpClientHandler</c> to use</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithHttpClientHandler(this Configuration configuration, HttpClientHandler httpClientHandler)
        {
            configuration.HttpClientHandler = httpClientHandler;
            return configuration;
        }

        /// <summary>
        /// Sets whether or not the streaming API should be used to receive flag updates. This
        /// is true by default. Streaming should only be disabled on the advice of LaunchDarkly
        /// support.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="enableStream">true if the streaming API should be used</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithIsStreamingEnabled(this Configuration configuration, bool enableStream)
        {
            configuration.IsStreamingEnabled = enableStream;
            return configuration;
        }

        /// <summary>
        /// Sets whether or not user attributes (other than the key) should be private (not sent to
        /// the LaunchDarkly server). If this is true, all of the user attributes will be private,
        /// not just the attributes specified with the <c>AndPrivate...</c> methods on the
        /// <see cref="User"/> object. By default, this is false.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="allAttributesPrivate">true if all attributes should be private</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithAllAttributesPrivate(this Configuration configuration, bool allAttributesPrivate)
        {
            configuration.AllAttributesPrivate = allAttributesPrivate;
            return configuration;
        }

        /// <summary>
        /// Marks an attribute name as private. Any users sent to LaunchDarkly with this
        /// configuration active will have attributes with this name removed, even if you did
        /// not use the <c>AndPrivate...</c> methods on the <see cref="User"/> object. You may
        /// call this method repeatedly to mark multiple attributes as private.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="attributeName">the attribute name</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithPrivateAttributeName(this Configuration configuration, string attributeName)
        {
            if (configuration.PrivateAttributeNames == null)
            {
                configuration.PrivateAttributeNames = new HashSet<string>();
            }
            configuration.PrivateAttributeNames.Add(attributeName);
            return configuration;
        }

        /// <summary>
        /// Sets the number of user keys that the event processor can remember at any one time, so that
        /// duplicate user details will not be sent in analytics events.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="capacity">the user key cache capacity</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithUserKeysCapacity(this Configuration configuration, int capacity)
        {
            configuration.UserKeysCapacity = capacity;
            return configuration;
        }

        /// <summary>
        /// Sets the interval at which the event processor will reset its set of known user keys. The
        /// default value is five minutes.
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="flushInterval">the flush interval</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithUserKeysFlushInterval(this Configuration configuration, TimeSpan flushInterval)
        {
            configuration.UserKeysFlushInterval = flushInterval;
            return configuration;
        }

        /// <summary>
        /// Sets whether to include full user details in every analytics event. The default is false (events will
        /// only include the user key, except for one "index" event that provides the full details for the user).
        /// </summary>
        /// <param name="configuration">the configuration</param>
        /// <param name="inlineUsers">true or false</param>
        /// <returns>the same <c>Configuration</c> instance</returns>
        [Obsolete(ObsoleteMessage)]
        public static Configuration WithInlineUsersInEvents(this Configuration configuration, bool inlineUsers)
        {
            configuration.InlineUsersInEvents = inlineUsers;
            return configuration;
        }
    }
}

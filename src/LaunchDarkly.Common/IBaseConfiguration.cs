using System;
using System.Collections.Generic;
using System.Net.Http;

namespace LaunchDarkly.Common
{
    /// <summary>
    /// Configuration properties that are used by both server-side and client-side SDKs.
    /// </summary>
    public interface IBaseConfiguration
    {
        /// <summary>
        /// The SDK key for your LaunchDarkly environment.
        /// </summary>
        string SdkKey { get; }

        /// <summary>
        /// The base URI of the LaunchDarkly server.
        /// </summary>
        Uri BaseUri { get; }

        /// <summary>
        /// The base URL of the LaunchDarkly analytics event server.
        /// </summary>
        Uri EventsUri { get; }

        /// <summary>
        /// Whether or not this client is offline. If true, no calls to Launchdarkly will be made.
        /// </summary>
        bool Offline { get; }

        /// <summary>
        /// The capacity of the events buffer. The client buffers up to this many events in
        /// memory before flushing. If the capacity is exceeded before the buffer is flushed,
        /// events will be discarded. Increasing the capacity means that events are less likely
        /// to be discarded, at the cost of consuming more memory.
        /// </summary>
        int EventQueueCapacity { get; }

        /// <summary>
        /// The time between flushes of the event buffer. Decreasing the flush interval means
        /// that the event buffer is less likely to reach capacity. The default value is 5 seconds.
        /// </summary>
        TimeSpan EventQueueFrequency { get; }
        
        /// <summary>
        /// Enables event sampling if non-zero. When set to the default of zero, all analytics events are
        /// sent back to LaunchDarkly. When greater than zero, there is a 1 in <c>EventSamplingInterval</c>
        /// chance that events will be sent (example: if the interval is 20, on average 5% of events will be sent).
        /// </summary>
        int EventSamplingInterval { get; }

        /// <summary>
        /// Whether or not user attributes (other than the key) should be private (not sent to
        /// the LaunchDarkly server). If this is true, all of the user attributes will be private,
        /// not just the attributes specified with the <c>AndPrivate...</c> methods on the
        /// <see cref="LaunchDarkly.Client.User"/> object. By default, this is false.
        /// </summary>
        bool AllAttributesPrivate { get; }

        /// <summary>
        /// Marks a set of attribute names as private. Any users sent to LaunchDarkly with this
        /// configuration active will have attributes with these names removed, even if you did
        /// not use the <c>AndPrivate...</c> methods on the <see cref="LaunchDarkly.Client.User"/> object.
        /// </summary>
        ISet<string> PrivateAttributeNames { get; }

        /// <summary>
        /// The number of user keys that the event processor can remember at any one time, so that
        /// duplicate user details will not be sent in analytics events.
        /// </summary>
        int UserKeysCapacity { get; }

        /// <summary>
        /// The interval at which the event processor will reset its set of known user keys. The
        /// default value is five minutes.
        /// </summary>
        TimeSpan UserKeysFlushInterval { get; }

        /// <summary>
        /// True if full user details should be included in every analytics event. The default is false (events will
        /// only include the user key, except for one "index" event that provides the full details for the user).
        /// </summary>
        bool InlineUsersInEvents { get; }

        /// <summary>
        /// The connection timeout. The default value is 10 seconds.
        /// </summary>
        TimeSpan HttpClientTimeout { get; }

        /// <summary>
        /// The object to be used for sending HTTP requests. This is exposed for testing purposes.
        /// </summary>
        HttpClientHandler HttpClientHandler { get; }
    }
}

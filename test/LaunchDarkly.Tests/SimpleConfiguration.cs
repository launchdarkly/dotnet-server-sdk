using System;
using System.Collections.Generic;
using System.Net.Http;
using LaunchDarkly.Client;

namespace LaunchDarkly.Tests
{
    // Used in unit tests of common code - a minimal implementation of IBaseConfiguration.
    class SimpleConfiguration : IBaseConfiguration
    {
        public string SdkKey { get; set; } = "SDK_KEY";
        public Uri BaseUri { get; set; }
        public Uri EventsUri { get; set; }
        public bool Offline { get; set; }
        public int EventQueueCapacity { get; set; }
        public TimeSpan EventQueueFrequency { get; set; }
        public int EventSamplingInterval { get; set; }
        public bool AllAttributesPrivate { get; set; }
        public ISet<string> PrivateAttributeNames { get; set; } = new HashSet<string>();
        public int UserKeysCapacity { get; set; }
        public TimeSpan UserKeysFlushInterval { get; set; }
        public bool InlineUsersInEvents { get; set; }
        public TimeSpan HttpClientTimeout { get; set; }
        public HttpClientHandler HttpClientHandler { get; set; }
    }
}

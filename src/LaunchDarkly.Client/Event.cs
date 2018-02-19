using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    internal abstract class Event
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [JsonProperty(PropertyName = "kind", NullValueHandling = NullValueHandling.Ignore)]
        public string Kind { get; private set; }
        
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser EventUser { get; private set; }

        [JsonProperty(PropertyName = "creationDate", NullValueHandling = NullValueHandling.Ignore)]
        public long CreationDate { get; private set; }

        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; private set; }
        
        internal Event(string kind, string key, EventUser eventUser)
        {
            Kind = kind;
            CreationDate = GetUnixTimestampMillis(DateTime.UtcNow);
            Key = key;
            EventUser = eventUser;
        }

        public static long GetUnixTimestampMillis(DateTime dateTime)
        {
            return (long) (dateTime - UnixEpoch).TotalMilliseconds;
        }
    }

    internal class FeatureRequestEvent : Event
    {
        [JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Value { get; private set; }

        [JsonProperty(PropertyName = "default", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Default { get; private set; }

        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Version { get; private set; }

        [JsonProperty(PropertyName = "prereqOf", NullValueHandling = NullValueHandling.Ignore)]
        public JToken PrereqOf { get; private set; }
        
        internal FeatureRequestEvent(string key, EventUser eventUser, JToken value, JToken defaultValue, JToken version,
            JToken prereqOf) : base("feature", key, eventUser)
        {
            Value = value;
            Default = defaultValue;
            Version = version;
            PrereqOf = prereqOf;
        }
    }

    internal class CustomEvent : Event
    {
        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
        public string Data { get; private set; }
        
        internal CustomEvent(string key, EventUser eventUser, string data) : base("custom", key, eventUser)
        {
            Data = data;
        }
    }

    internal class IdentifyEvent : Event
    {
        internal IdentifyEvent(EventUser eventUser) : base("identify", eventUser.Key, eventUser)
        {
        }
    }
}
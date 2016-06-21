using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    public abstract class Event
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        [JsonProperty(PropertyName = "kind", NullValueHandling = NullValueHandling.Ignore)]
        public string Kind { get; private set; }
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        public User User { get; private set; }
        [JsonProperty(PropertyName = "creationDate", NullValueHandling = NullValueHandling.Ignore)]
        public long CreationDate { get; private set; }
        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; private set; }

        protected Event(string kind, string key, User user)
        {
            Kind = kind;
            CreationDate = GetUnixTimestampMillis(DateTime.UtcNow);
            Key = key;
            User = user;
        }

        public static long GetUnixTimestampMillis(DateTime dateTime)
        {
            return (long)(dateTime - UnixEpoch).TotalMilliseconds;
        }
    }

    public class FeatureRequestEvent : Event
    {
        [JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Value { get; private set; }
        [JsonProperty(PropertyName = "default", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Default { get; private set; }

        public FeatureRequestEvent(string key, User user, JToken value, JToken defaultValue) : base("feature", key, user)
        {
            Value = value;
            Default = defaultValue;
        }
    }

    public class CustomEvent: Event
    {
        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
        public string Data { get; private set; }

        public CustomEvent(string key, User user, string data) : base("custom", key, user)
        {
            Data = data;
        }
    }

    public class IdentifyEvent: Event
    {
        public IdentifyEvent(User user) : base("identify", user.Key, user) { }
    }
}

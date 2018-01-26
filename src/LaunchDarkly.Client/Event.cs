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

        [Obsolete]
        [JsonIgnore]
        public User User { get; private set; }

        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser EventUser { get; private set; }

        [JsonProperty(PropertyName = "creationDate", NullValueHandling = NullValueHandling.Ignore)]
        public long CreationDate { get; private set; }

        [JsonProperty(PropertyName = "key", NullValueHandling = NullValueHandling.Ignore)]
        public string Key { get; private set; }

        [Obsolete]
        protected Event(string kind, string key, User user) : this(kind, key, null, user)
        {
        }

        internal Event(string kind, string key, EventUser eventUser, User user)
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

    public class FeatureRequestEvent : Event
    {
        [JsonProperty(PropertyName = "value", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Value { get; private set; }

        [JsonProperty(PropertyName = "default", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Default { get; private set; }

        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Version { get; private set; }

        [JsonProperty(PropertyName = "prereqOf", NullValueHandling = NullValueHandling.Ignore)]
        public JToken PrereqOf { get; private set; }

        [Obsolete]
        public FeatureRequestEvent(string key, User user, JToken value, JToken defaultValue, JToken version,
            JToken prereqOf) : this(key, null, user, value, defaultValue, version, prereqOf)
        {
            Value = value;
            Default = defaultValue;
            Version = version;
            PrereqOf = prereqOf;
        }

        internal FeatureRequestEvent(string key, EventUser eventUser, User user, JToken value, JToken defaultValue, JToken version,
            JToken prereqOf) : base("feature", key, eventUser, user)
        {
            Value = value;
            Default = defaultValue;
            Version = version;
            PrereqOf = prereqOf;
        }
    }

    public class CustomEvent : Event
    {
        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
        public string Data { get; private set; }

        public CustomEvent(string key, User user, string data) : this(key, null, user, data)
        {
        }

        internal CustomEvent(string key, EventUser eventUser, User user, string data) : base("custom", key, eventUser, user)
        {
            Data = data;
        }
    }

    public class IdentifyEvent : Event
    {
        public IdentifyEvent(User user) : this(null, user)
        {
        }

        internal IdentifyEvent(EventUser eventUser, User user) : base("identify", user.Key, eventUser, user)
        {
        }
}
}
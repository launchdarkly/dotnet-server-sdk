using System;

namespace LaunchDarkly.Client
{
    public abstract class Event
    {
        public string Kind { get; private set; }
        public User User { get; private set; }
        public long CreationDate { get; private set; }
        public string Key { get; private set; }

        protected Event(string kind, string key, User user)
        {
            Kind = kind;
            CreationDate = ToUnixTime(DateTime.Now);
            Key = key;
            User = user;
        }

        private static long ToUnixTime(DateTime date)
        {
            return (date.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
        }
    }

    public class FeatureRequestEvent<T> : Event
    {
        public T Value { get; private set; }
        public Boolean Default { get; private set; }

        public FeatureRequestEvent(String key, User user, T value, Boolean defaultValueUsed) : base("feature", key, user)
        {
            Value = value;
            Default = defaultValueUsed;
        }
    }

    public class CustomEvent: Event
    {
        public string Data { get; private set; }

        public CustomEvent(String key, User user, string data) : base("custom", key, user)
        {
            Data = data;
        }
    }
}

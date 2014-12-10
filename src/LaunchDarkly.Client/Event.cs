using System;

namespace LaunchDarkly.Client
{
    public abstract class Event
    {
        private long timeStamp;
        public string Key { get; private set; }
        public User User { get; private set; }

        protected Event(string key, User user)
        {
            timeStamp = ToUnixTime(DateTime.Now);
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
        private T _value;

        public FeatureRequestEvent(String key, User user, T value) : base(key, user)
        {
            _value = value;
        }
    }

    public class CustomEvent: Event
    {
        private string _data;

        public CustomEvent(String key, User user, string data) : base(key, user)
        {
            _data = data;
        }
    }
}

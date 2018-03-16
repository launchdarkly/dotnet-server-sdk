using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    internal sealed class EventSummarizer
    {
        private SummaryState _eventsState;
        private LRUCacheSet<string> _userKeys;

        public EventSummarizer(Configuration config)
        {
            _eventsState = new SummaryState();
            _userKeys = new LRUCacheSet<string>(config.UserKeysCapacity);
        }

        /// <summary>
        /// Adds to the set of users we've noticed, and returns true if the user was already known to us.
        /// </summary>
        /// <param name="user">a user</param>
        /// <returns>true if we've already seen this user</returns>
        internal bool NoticeUser(User user)
        {
            if (user == null || user.Key == null)
            {
                return false;
            }
            return _userKeys.Add(user.Key);
        }

        /// <summary>
        /// Resets the set of users we've seen.
        /// </summary>
        internal void ResetUsers()
        {
            _userKeys.Clear();
        }

        /// <summary>
        /// Adds this event to our counters, if it is a type of event we need to count.
        /// </summary>
        /// <param name="e">an event</param>
        internal void SummarizeEvent(Event e)
        {
            if (e is FeatureRequestEvent fe)
            {
                _eventsState.IncrementCounter(fe.Key, fe.Variation, fe.Version, fe.Value, fe.Default);
                _eventsState.NoteTimestamp(fe.CreationDate);
            }
        }

        /// <summary>
        /// Returns a snapshot of the current summarized event data, and resets this state.
        /// </summary>
        /// <returns>the previous event state</returns>
        internal SummaryState Snapshot()
        {
            SummaryState ret = _eventsState;
            _eventsState = new SummaryState();
            return ret;
        }

        internal SummaryOutput Output(SummaryState snapshot)
        {
            Dictionary<string, EventSummaryFlag> flagsOut = new Dictionary<string, EventSummaryFlag>();
            foreach (KeyValuePair<EventsCounterKey, EventsCounterValue> entry in snapshot.Counters)
            {
                EventSummaryFlag flag;
                if (!flagsOut.TryGetValue(entry.Key.Key, out flag))
                {
                    flag = new EventSummaryFlag(entry.Value.Default, new List<EventSummaryCounter>());
                    flagsOut[entry.Key.Key] = flag;
                }
                flag.Counters.Add(new EventSummaryCounter(entry.Value.FlagValue, entry.Key.Version,
                    entry.Value.Count));
            }
            return new SummaryOutput(snapshot.StartDate, snapshot.EndDate, flagsOut);
        }
    }

    internal sealed class SummaryState
    {
        internal Dictionary<EventsCounterKey, EventsCounterValue> Counters { get; } =
            new Dictionary<EventsCounterKey, EventsCounterValue>();
        internal long StartDate { get; private set; }
        internal long EndDate { get; private set; }
        internal bool Empty
        {
            get  
            {
                return Counters.Count == 0;
            }
        }

        internal void IncrementCounter(string key, int? variation, int? version, JToken flagValue, JToken defaultVal)
        {
            EventsCounterKey counterKey = new EventsCounterKey(key, variation, version);
            if (Counters.TryGetValue(counterKey, out EventsCounterValue value))
            {
                value.Increment();
            }
            else
            {
                Counters[counterKey] = new EventsCounterValue(1, flagValue, defaultVal);
            }
        }

        internal void NoteTimestamp(long timestamp)
        {
            if (StartDate == 0 || timestamp < StartDate)
            {
                StartDate = timestamp;
            }
            if (timestamp > EndDate)
            {
                EndDate = timestamp;
            }
        }
    }
    
    internal sealed class EventsCounterKey
    {
        internal readonly string Key;
        internal readonly int? Variation;
        internal readonly int? Version;

        internal EventsCounterKey(string key, int? variation, int? version)
        {
            Key = key;
            Variation = variation;
            Version = version;
        }

        public override bool Equals(object obj)
        {
            if (obj is EventsCounterKey o)
            {
                return Key == o.Key && Variation == o.Variation && Version == o.Version;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode() + 31 * (Variation.GetHashCode() + 31 * Version.GetHashCode());
        }
    }

    internal sealed class EventsCounterValue
    {
        internal int Count;
        internal readonly JToken FlagValue;
        internal readonly JToken Default;

        internal EventsCounterValue(int count, JToken flagValue, JToken defaultVal)
        {
            Count = count;
            FlagValue = flagValue;
            Default = defaultVal;
        }

        internal void Increment()
        {
            Count++;
        }
    }

    internal sealed class EventSummaryFlag
    {
        [JsonProperty(PropertyName = "default")]
        internal JToken Default { get; private set; }
        [JsonProperty(PropertyName = "counters")]
        internal List<EventSummaryCounter> Counters { get; private set; }

        internal EventSummaryFlag(JToken defaultVal, List<EventSummaryCounter> counters)
        {
            Default = defaultVal;
            Counters = counters;
        }

        // Used only in tests
        public override bool Equals(object obj)
        {
            if (obj is EventSummaryFlag o)
            {
                bool se = Counters.SequenceEqual(o.Counters);
                return Object.Equals(Default, o.Default) && Counters.SequenceEqual(o.Counters);
            }
            return false;
        }

        // Used only in tests
        public override int GetHashCode()
        {
            return (Default == null ? 0 : Default.GetHashCode()) + 31 * Counters.GetHashCode();
        }

        // Used only in tests
        public override string ToString()
        {
            return "{" + Default + ", " + String.Join(", ", Counters) + "}";
        }
    }

    internal sealed class EventSummaryCounter
    {
        [JsonProperty(PropertyName = "value")]
        internal JToken Value { get; private set; }
        [JsonProperty(PropertyName = "version")]
        internal int? Version { get; private set; }
        [JsonProperty(PropertyName = "count")]
        internal int Count { get; private set; }
        [JsonProperty(PropertyName = "unknown", NullValueHandling = NullValueHandling.Ignore)]
        internal bool? Unknown { get; private set; }

        internal EventSummaryCounter(JToken value, int? version, int count)
        {
            Value = value;
            Version = version;
            Count = count;
            if (version == null)
            {
                Unknown = true;
            }
        }

        // Used only in tests
        public override bool Equals(object obj)
        {
            if (obj is EventSummaryCounter o)
            {
                return Object.Equals(Value, o.Value) && Version == o.Version && Count == o.Count
                    && Unknown == o.Unknown;
            }
            return false;
        }

        // Used only in tests
        public override int GetHashCode()
        {
            return (Value == null ? 0 : Value.GetHashCode()) + 31 *
                (Version.GetHashCode() + 31 * (Count + 31 * Unknown.GetHashCode()));
        }

        // Used only in tests
        public override string ToString()
        {
            return "{" + Value + ", " + Version + ", " + Count + "}";
        }
    }

    internal sealed class SummaryOutput
    {
        [JsonProperty(PropertyName = "startDate")]
        internal long StartDate { get; private set; }
        [JsonProperty(PropertyName = "endDate")]
        internal long EndDate { get; private set; }
        [JsonProperty(PropertyName = "features")]
        internal Dictionary<string, EventSummaryFlag> Features { get; private set; }

        internal SummaryOutput(long startDate, long endDate, Dictionary<string, EventSummaryFlag> features)
        {
            StartDate = startDate;
            EndDate = endDate;
            Features = features;
        }
    }
}

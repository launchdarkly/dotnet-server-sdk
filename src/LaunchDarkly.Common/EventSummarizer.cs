using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Client;

namespace LaunchDarkly.Common
{
    internal sealed class EventSummarizer
    {
        private EventSummary _eventsState;

        public EventSummarizer()
        {
            _eventsState = new EventSummary();
        }
        
        // Adds this event to our counters, if it is a type of event we need to count.
        internal void SummarizeEvent(Event e)
        {
            if (e is FeatureRequestEvent fe)
            {
                _eventsState.IncrementCounter(fe.Key, fe.Variation, fe.Version, fe.Value, fe.Default);
                _eventsState.NoteTimestamp(fe.CreationDate);
            }
        }

        // Returns the current summarized event data.
        internal EventSummary Snapshot()
        {
            EventSummary ret = _eventsState;
            _eventsState = new EventSummary();
            return ret;
        }

        internal void Clear()
        {
            _eventsState = new EventSummary();
        }
    }

    internal sealed class EventSummary
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
            EventsCounterKey counterKey = new EventsCounterKey(key, version, variation);
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
        internal readonly int? Version;
        internal readonly int? Variation;

        internal EventsCounterKey(string key, int? version, int? variation)
        {
            Key = key;
            Version = version;
            Variation = variation;
        }

        // Required because we use this class as a dictionary key
        public override bool Equals(object obj)
        {
            if (obj is EventsCounterKey o)
            {
                return Key == o.Key && Variation == o.Variation && Version == o.Version;
            }
            return false;
        }

        // Required because we use this class as a dictionary key
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

        // Used only in tests
        public override bool Equals(object obj)
        {
            if (obj is EventsCounterValue o)
            {
                return Count == o.Count && Object.Equals(FlagValue, o.FlagValue) && Object.Equals(Default, o.Default);
            }
            return false;
        }

        // Used only in tests
        public override int GetHashCode()
        {
            return Count + 31 * ((FlagValue == null ? 0 : FlagValue.GetHashCode()) + 31 *
                (Default == null ? 0 : Default.GetHashCode()));
        }

        // Used only in tests
        public override string ToString()
        {
            return "{" + Count + ", " + FlagValue + ", " + Default + "}";
        }
    }
}

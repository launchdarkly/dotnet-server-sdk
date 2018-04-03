using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    // Base class for data structures that we send in an event payload, which are somewhat
    // different in shape from the originating events.  Also defines all of its own subclasses
    // and the class that constructs them.  These are implementation details used only by
    // DefaultEventProcessor and related classes, so they are all internal.
    internal abstract class EventOutput
    {
        [JsonProperty(PropertyName = "kind")]
        internal string Kind { get; set; }
    }

    internal sealed class FeatureRequestEventOutput : EventOutput
    {
        [JsonProperty(PropertyName = "creationDate")]
        internal long CreationDate { get; set; }
        [JsonProperty(PropertyName = "key")]
        internal string Key { get; set; }
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser User { get; set; }
        [JsonProperty(PropertyName = "userKey", NullValueHandling = NullValueHandling.Ignore)]
        internal string UserKey { get; set; }
        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        internal int? Version { get; set; }
        [JsonProperty(PropertyName = "value")]
        internal JToken Value { get; set; }
        [JsonProperty(PropertyName = "default", NullValueHandling = NullValueHandling.Ignore)]
        internal JToken Default { get; set; }
        [JsonProperty(PropertyName = "prereqOf", NullValueHandling = NullValueHandling.Ignore)]
        internal string PrereqOf { get; set; }
    }

    internal sealed class IdentifyEventOutput : EventOutput
    {
        [JsonProperty(PropertyName = "creationDate")]
        internal long CreationDate { get; set; }
        [JsonProperty(PropertyName = "key")]
        internal string Key { get; set; }
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser User { get; set; }
    }

    internal sealed class CustomEventOutput : EventOutput
    {
        [JsonProperty(PropertyName = "creationDate")]
        internal long CreationDate { get; set; }
        [JsonProperty(PropertyName = "key")]
        internal string Key { get; set; }
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser User { get; set; }
        [JsonProperty(PropertyName = "userKey", NullValueHandling = NullValueHandling.Ignore)]
        internal string UserKey { get; set; }
        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
        internal JToken Data { get; set; }
    }

    internal sealed class IndexEventOutput : EventOutput
    {
        [JsonProperty(PropertyName = "creationDate")]
        internal long CreationDate { get; set; }
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser User { get; set; }
    }

    internal sealed class SummaryEventOutput : EventOutput
    {
        [JsonProperty(PropertyName = "kind")]
        internal string Kind { get; set; } = "summary";
        [JsonProperty(PropertyName = "startDate")]
        internal long StartDate { get; set; }
        [JsonProperty(PropertyName = "endDate")]
        internal long EndDate { get; set; }
        [JsonProperty(PropertyName = "features")]
        internal Dictionary<string, EventSummaryFlag> Features;
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
    }

    internal sealed class EventOutputFormatter
    {
        private readonly Configuration _config;

        internal EventOutputFormatter(Configuration config)
        {
            _config = config;
        }

        internal List<EventOutput> MakeOutputEvents(Event[] events, EventSummary summary)
        {
            List<EventOutput> eventsOut = new List<EventOutput>(events.Length + 1);
            foreach (Event e in events)
            {
                EventOutput eo = MakeOutputEvent(e);
                if (eo != null)
                {
                    eventsOut.Add(eo);
                }
            }
            if (summary.Counters.Count > 0)
            {
                eventsOut.Add(MakeSummaryEvent(summary));
            }
            return eventsOut;
        }

        private EventOutput MakeOutputEvent(Event e)
        {
            if (e is FeatureRequestEvent fe)
            {
                bool debug = !fe.TrackEvents && fe.DebugEventsUntilDate != null;
                return new FeatureRequestEventOutput
                {
                    Kind = debug ? "debug" : "feature",
                    CreationDate = fe.CreationDate,
                    Key = fe.Key,
                    User = _config.InlineUsersInEvents ? EventUser.FromUser(fe.User, _config) : null,
                    UserKey = _config.InlineUsersInEvents ? null : fe.User.Key,
                    Version = fe.Version,
                    Value = fe.Value,
                    Default = fe.Default,
                    PrereqOf = fe.PrereqOf
                };
            }
            else if (e is IdentifyEvent)
            {
                return new IdentifyEventOutput
                {
                    Kind = "identify",
                    CreationDate = e.CreationDate,
                    Key = e.User.Key,
                    User = EventUser.FromUser(e.User, _config)
                };
            }
            else if (e is CustomEvent ce)
            {
                return new CustomEventOutput
                {
                    Kind = "custom",
                    CreationDate = ce.CreationDate,
                    Key = ce.Key,
                    User = _config.InlineUsersInEvents ? EventUser.FromUser(ce.User, _config) : null,
                    UserKey = _config.InlineUsersInEvents ? null : ce.User.Key,
                    Data = ce.Data
                };
            }
            else if (e is IndexEvent)
            {
                return new IndexEventOutput
                {
                    Kind = "index",
                    CreationDate = e.CreationDate,
                    User = EventUser.FromUser(e.User, _config)
                };
            }
            return null;
        }

        // Transform the summary data into the format used in event sending.
        private SummaryEventOutput MakeSummaryEvent(EventSummary summary)
        {
            Dictionary<string, EventSummaryFlag> flagsOut = new Dictionary<string, EventSummaryFlag>();
            foreach (KeyValuePair<EventsCounterKey, EventsCounterValue> entry in summary.Counters)
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
            return new SummaryEventOutput
            {
                Kind = "summary",
                StartDate = summary.StartDate,
                EndDate = summary.EndDate,
                Features = flagsOut
            };
        }
    }
}

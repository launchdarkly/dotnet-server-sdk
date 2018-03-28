using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    internal sealed class DefaultEventProcessor : IEventProcessor
    {
        internal static readonly ILog Log = LogManager.GetLogger(typeof(DefaultEventProcessor));

        private readonly BlockingCollection<IEventMessage> _messageQueue;
        private readonly EventConsumer _consumer;
        private readonly Timer _flushTimer;
        private readonly Timer _flushUsersTimer;
        private AtomicBoolean _stopped;
        private AtomicBoolean _inputCapacityExceeded;

        internal DefaultEventProcessor(Configuration config)
        {
            _messageQueue = new BlockingCollection<IEventMessage>(config.EventQueueCapacity);
            _consumer = new EventConsumer(config, _messageQueue);
            _flushTimer = new Timer(DoBackgroundFlush, null, config.EventQueueFrequency,
                config.EventQueueFrequency);
            _flushUsersTimer = new Timer(DoUserKeysFlush, null, config.UserKeysFlushInterval,
                config.UserKeysFlushInterval);
            _stopped = new AtomicBoolean(false);
            _inputCapacityExceeded = new AtomicBoolean(false);
        }

        void IEventProcessor.SendEvent(Event eventToLog)
        {
            SubmitMessage(new EventMessage(eventToLog));
        }

        void IEventProcessor.Flush()
        {
            SubmitMessage(new FlushMessage());
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_stopped.GetAndSet(true))
                {
                    _flushTimer.Dispose();
                    _flushUsersTimer.Dispose();
                    SubmitMessage(new FlushMessage());
                    ShutdownMessage message = new ShutdownMessage();
                    SubmitMessage(message);
                    message.WaitForCompletion();
                    ((IDisposable)_consumer).Dispose();
                    _messageQueue.CompleteAdding();
                    _messageQueue.Dispose();
                }
            }
        }

        private bool SubmitMessage(IEventMessage message)
        {
            try
            {
                if (_messageQueue.TryAdd(message))
                {
                    _inputCapacityExceeded.GetAndSet(false);
                }
                else
                {
                    // This doesn't mean that the output event buffer is full, but rather that the main thread is
                    // seriously backed up with not-yet-processed events. We shouldn't see this.
                    if (!_inputCapacityExceeded.GetAndSet(true))
                    {
                        Log.Warn("Events are being produced faster than they can be processed");
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // queue has been shut down
                return false;
            }
            return true;
        }

        // exposed for testing
        internal void WaitUntilInactive()
        {
            TestSyncMessage message = new TestSyncMessage();
            SubmitMessage(message);
            message.WaitForCompletion();
        }

        private void DoBackgroundFlush(object StateInfo)
        {
            SubmitMessage(new FlushMessage());
        }

        private void DoUserKeysFlush(object StateInfo)
        {
            SubmitMessage(new FlushUsersMessage());
        }
    }

    internal class AtomicBoolean
    {
        internal int _value;

        internal AtomicBoolean(bool value)
        {
            _value = value ? 1 : 0;
        }

        internal bool GetAndSet(bool newValue)
        {
            int old = Interlocked.Exchange(ref _value, newValue ? 1 : 0);
            return old != 0;
        }
    }

    internal interface IEventMessage { }

    internal class EventMessage : IEventMessage
    {
        internal Event Event { get; private set; }

        internal EventMessage(Event e)
        {
            Event = e;
        }
    }

    internal class FlushMessage : IEventMessage { }

    internal class FlushUsersMessage : IEventMessage { }

    internal class SynchronousMessage : IEventMessage
    {
        internal readonly Semaphore _reply;
        
        internal SynchronousMessage()
        {
            _reply = new Semaphore(0, 1);
        }
        
        internal void WaitForCompletion()
        {
            _reply.WaitOne();
        }

        internal void Completed()
        {
            _reply.Release();
        }
    }

    internal class TestSyncMessage : SynchronousMessage { }

    internal class ShutdownMessage : SynchronousMessage { }
    
    internal sealed class EventConsumer : IDisposable
    {
        private readonly Configuration _config;
        private readonly BlockingCollection<IEventMessage> _messageQueue;
        private readonly List<Event> _eventQueue;
        private readonly EventSummarizer _summarizer;
        private readonly LRUCacheSet<string> _userKeys;
        private readonly CountdownEvent _flushWorkersCounter;
        private readonly HttpClient _httpClient;
        private readonly Uri _uri;
        private readonly Random _random;
        private bool exceededCapacity;
        private long _lastKnownPastTime;
        private volatile bool _disabled;

        internal EventConsumer(Configuration config, BlockingCollection<IEventMessage> messageQueue)
        {
            _config = config;
            _messageQueue = messageQueue;
            _summarizer = new EventSummarizer();
            _userKeys = new LRUCacheSet<string>(config.UserKeysCapacity);
            _flushWorkersCounter = new CountdownEvent(1);
            _httpClient = config.HttpClient();
            _eventQueue = new List<Event>();
            _uri = new Uri(_config.EventsUri.AbsoluteUri + "bulk");
            _random = new Random();
            Task.Run(() => RunMainLoop());
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
        }

        private void RunMainLoop()
        {
            bool running = true;
            while (running)
            {
                IEventMessage message = _messageQueue.Take();
                if (message is EventMessage em)
                {
                    ProcessEvent(em.Event);
                }
                else if (message is FlushMessage)
                {
                    StartFlush();
                }
                else if (message is FlushUsersMessage)
                {
                    _userKeys.Clear();
                }
                else if (message is TestSyncMessage tm)
                {
                    WaitForFlushes();
                    tm.Completed();
                }
                else if (message is ShutdownMessage sm)
                {
                    WaitForFlushes();
                    running = false;
                    sm.Completed();
                }
            }
        }

        private void WaitForFlushes()
        {
            _flushWorkersCounter.Signal();
            _flushWorkersCounter.Wait();
            _flushWorkersCounter.Reset(1);
        }

        private void ProcessEvent(Event e)
        {
            if (_disabled)
            {
                return;
            }

            // For each user we haven't seen before, we add an index event - unless this is already
            // an identify event for that user.
            if (!_config.InlineUsersInEvents && e.User != null && !NoticeUser(e.User))
            {
                if (!(e is IdentifyEvent))
                {
                    IndexEvent ie = new IndexEvent(e.CreationDate, e.User);
                    QueueEvent(ie);
                }
            }

            // Always record the event in the summarizer.
            _summarizer.SummarizeEvent(e);

            if (ShouldTrackFullEvent(e))
            {
                // Sampling interval applies only to fully-tracked events.
                if (_config.EventSamplingInterval > 1 && _random.Next(_config.EventSamplingInterval) != 0)
                {
                    return;
                }
                // Queue the event as-is; we'll transform it into an output event when we're flushing
                // (to avoid doing that work on our main thread).
                QueueEvent(e);
            }
        }

        private void QueueEvent(Event e)
        {
            if (_eventQueue.Count >= _config.EventQueueCapacity)
            {
                if (!exceededCapacity)
                {
                    DefaultEventProcessor.Log.Warn("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
                    exceededCapacity = true;
                }
            }
            else
            {
                _eventQueue.Add(e);
                exceededCapacity = false;
            }
        }

        /// <summary>
        /// Adds to the set of users we've noticed, and returns true if the user was already known to us.
        /// </summary>
        /// <param name="user">a user</param>
        /// <returns>true if we've already seen this user</returns>
        private bool NoticeUser(User user)
        {
            if (user == null || user.Key == null)
            {
                return false;
            }
            return _userKeys.Add(user.Key);
        }

        private bool ShouldTrackFullEvent(Event e)
        {
            if (e is FeatureRequestEvent fe)
            {
                if (fe.TrackEvents)
                {
                    return true;
                }
                if (fe.DebugEventsUntilDate != null)
                {
                    long lastPast = Interlocked.Read(ref _lastKnownPastTime);
                    if (fe.DebugEventsUntilDate > lastPast &&
                        fe.DebugEventsUntilDate > Util.GetUnixTimestampMillis(DateTime.Now))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        private IEventOutput MakeEventOutput(Event e)
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
                    CreationDate = e.CreationDate,
                    Key = e.User.Key,
                    User = EventUser.FromUser(e.User, _config)
                };
            }
            else if (e is CustomEvent ce)
            {
                return new CustomEventOutput
                {
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
                StartDate = summary.StartDate,
                EndDate = summary.EndDate,
                Features = flagsOut
            };
        }

        /// <summary>
        /// Grabs a snapshot of the current internal state, and starts a new thread to send it to the server
        /// (if there's anything to send).
        /// </summary>
        private void StartFlush()
        {
            if (_disabled)
            {
                return;
            }

            EventSummary snapshot = _summarizer.Snapshot();
            Event[] events = _eventQueue.ToArray();
            _eventQueue.Clear();
            if (events.Length > 0 || !snapshot.Empty)
            {
                _flushWorkersCounter.AddCount();
                Task.Run(() => FlushEventsAsync(events, snapshot));
            }
        }

        private async Task FlushEventsAsync(Event[] events, EventSummary snapshot)
        {
            List<IEventOutput> eventsOut = new List<IEventOutput>(events.Length + 1);
            foreach (Event e in events)
            {
                IEventOutput eo = MakeEventOutput(e);
                if (eo != null)
                {
                    eventsOut.Add(eo);
                }
            }
            if (snapshot.Counters.Count > 0)
            {
                eventsOut.Add(MakeSummaryEvent(snapshot));
            }
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            var jsonEvents = JsonConvert.SerializeObject(eventsOut, Formatting.None);
            try
            {
                await SendEventsAsync(jsonEvents, eventsOut.Count, cts);
            }
            catch (Exception e)
            {
                DefaultEventProcessor.Log.DebugFormat("Error sending events: {0} waiting 1 second before retrying.",
                    e, Util.ExceptionMessage(e));

                Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                cts = new CancellationTokenSource(_config.HttpClientTimeout);
                try
                {
                    await SendEventsAsync(jsonEvents, eventsOut.Count, cts);
                }
                catch (TaskCanceledException tce)
                {
                    if (tce.CancellationToken == cts.Token)
                    {
                        //Indicates the task was cancelled by something other than a request timeout
                        DefaultEventProcessor.Log.ErrorFormat("Error Submitting Events using uri: '{0}' '{1}'",
                            tce, _uri.AbsoluteUri, Util.ExceptionMessage(tce));
                    }
                    else
                    {
                        //Otherwise this was a request timeout.
                        DefaultEventProcessor.Log.ErrorFormat("Timed out trying to send {0} events after {1}",
                            tce, eventsOut.Count, _config.HttpClientTimeout);
                    }
                }
                catch (Exception ex)
                {
                    DefaultEventProcessor.Log.ErrorFormat("Error Submitting Events using uri: '{0}' '{1}'",
                        ex,
                        _uri.AbsoluteUri,
                         Util.ExceptionMessage(ex));
                }
            }
            _flushWorkersCounter.Signal();
        }

        private async Task SendEventsAsync(String jsonEvents, int count, CancellationTokenSource cts)
        {
            DefaultEventProcessor.Log.DebugFormat("Submitting {0} events to {1} with json: {2}",
                count, _uri.AbsoluteUri, jsonEvents);

            using (var stringContent = new StringContent(jsonEvents, Encoding.UTF8, "application/json"))
            using (var response = await _httpClient.PostAsync(_uri, stringContent).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    DefaultEventProcessor.Log.ErrorFormat("Error Submitting Events using uri: '{0}'; Status: '{1}'",
                        _uri.AbsoluteUri,
                        response.StatusCode);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        DefaultEventProcessor.Log.Error("Received 401 error, no further events will be posted since SDK key is invalid");
                        _disabled = true;
                    }
                }
                else
                {
                    DefaultEventProcessor.Log.DebugFormat("Got {0} when sending events.",
                        response.StatusCode);
                    DateTimeOffset? respDate = response.Headers.Date;
                    if (respDate.HasValue)
                    {
                        Interlocked.Exchange(ref _lastKnownPastTime,
                            Util.GetUnixTimestampMillis(respDate.Value.DateTime));
                    }
                }
            }
        }
    }

    internal interface IEventOutput { }

    internal class FeatureRequestEventOutput : IEventOutput
    {
        [JsonProperty(PropertyName = "kind")]
        internal string Kind { get; set; }
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

    internal class IdentifyEventOutput : IEventOutput
    {
        [JsonProperty(PropertyName = "kind")]
        internal string Kind { get; set; } = "identify";
        [JsonProperty(PropertyName = "creationDate")]
        internal long CreationDate { get; set; }
        [JsonProperty(PropertyName = "key")]
        internal string Key { get; set; }
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser User { get; set; }
    }

    internal class CustomEventOutput : IEventOutput
    {
        [JsonProperty(PropertyName = "kind")]
        internal string Kind { get; set; } = "custom";
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

    internal class IndexEvent : Event
    {
        internal IndexEvent(long creationDate, User user) :
            base(creationDate, user.Key, user)
        { }
    }

    internal class IndexEventOutput : IEventOutput
    {
        [JsonProperty(PropertyName = "kind")]
        internal string Kind { get; set; } = "index";
        [JsonProperty(PropertyName = "creationDate")]
        internal long CreationDate { get; set; }
        [JsonProperty(PropertyName = "user", NullValueHandling = NullValueHandling.Ignore)]
        internal EventUser User { get; set; }
    }

    internal class SummaryEventOutput : IEventOutput
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
}
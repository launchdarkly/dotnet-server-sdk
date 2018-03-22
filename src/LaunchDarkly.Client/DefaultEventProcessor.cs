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
        private static readonly ILog Log = LogManager.GetLogger(typeof(DefaultEventProcessor));

        private readonly Configuration _config;
        private readonly BlockingCollection<IEventMessage> _messageQueue;
        private readonly List<Event> _eventQueue;
        private readonly EventSummarizer _summarizer;
        private readonly Timer _timer1;
        private readonly Timer _timer2;
        private readonly HttpClient _httpClient;
        private readonly Uri _uri;
        private readonly Random _random;
        private bool exceededCapacity;
        private long _lastKnownPastTime;
        private volatile bool _shutdown;

        internal DefaultEventProcessor(Configuration config)
        {
            _config = config;
            _summarizer = new EventSummarizer(config);
            _httpClient = config.HttpClient();
            _messageQueue = new BlockingCollection<IEventMessage>(_config.EventQueueCapacity);
            _eventQueue = new List<Event>();
            _timer1 = new Timer(DoBackgroundFlush, null, _config.EventQueueFrequency,
                _config.EventQueueFrequency);
            _timer2 = new Timer(DoUserKeysFlush, null, _config.UserKeysFlushInterval,
                _config.UserKeysFlushInterval);
            _uri = new Uri(_config.EventsUri.AbsoluteUri + "bulk");
            _random = new Random();
            Task.Run(() => RunMainLoop());
        }

        private void DoBackgroundFlush(object StateInfo)
        {
            SubmitMessage(new FlushMessage());
        }

        private void DoUserKeysFlush(object StateInfo)
        {
            SubmitMessage(new FlushUsersMessage());
        }

        void IEventProcessor.SendEvent(Event eventToLog)
        {
            EventMessage message = new EventMessage
            {
                Event = eventToLog
            };
            SubmitMessage(message);
        }

        void IEventProcessor.Flush()
        {
            FlushMessage message = new FlushMessage
            {
                Reply = new Semaphore(0, 1)
            };
            if (SubmitMessage(message))
            {
                message.Reply.WaitOne();
            }
        }

        private bool SubmitMessage(IEventMessage message)
        {
            if (_shutdown)
            {
                return false;
            }
            try
            {
                _messageQueue.Add(message);
            }
            catch (InvalidOperationException)
            {
                // queue has been shut down
                return false;
            }
            return true;
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
                ((IEventProcessor)this).Flush();
                _shutdown = true;
                _messageQueue.CompleteAdding();
                _timer1.Dispose();
                _timer2.Dispose();
                _messageQueue.Dispose();
            }
        }
        
        private void RunMainLoop()
        {
            while (!_shutdown)
            {
                IEventMessage message = _messageQueue.Take();
                if (message is EventMessage)
                {
                    ProcessEvent((message as EventMessage).Event);
                }
                else if (message is FlushMessage)
                {
                    DispatchFlush((message as FlushMessage).Reply);
                }
                else if (message is FlushUsersMessage)
                {
                    _summarizer.ResetUsers();
                }
                else if (message is ShutdownMessage)
                {
                    _shutdown = true;
                }
            }
        }

        private void ProcessEvent(Event e)
        {
            // For each user we haven't seen before, we add an index event - unless this is already
            // an identify event for that user.
            if (!_config.InlineUsersInEvents && e.User != null && !_summarizer.NoticeUser(e.User))
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
                    Log.Warn("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
                    exceededCapacity = true;
                }
            }
            else
            {
                _eventQueue.Add(e);
                exceededCapacity = false;
            }
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

        /// <summary>
        /// Grabs a snapshot of the current internal state, and starts a new thread to send it to the server
        /// (if there's anything to send).
        /// </summary>
        /// <param name="reply">If non-null, we will use this semaphore to tell the caller when we're done.</param>
        private void DispatchFlush(Semaphore reply)
        {
            SummaryState snapshot = _summarizer.Snapshot();
            Event[] events = _eventQueue.ToArray();
            _eventQueue.Clear();
            if (events.Length > 0 || !snapshot.Empty)
            {
                Task.Run(() => FlushEventsAsync(events, snapshot, reply));
            }
            else
            {
                if (reply != null)
                {
                    reply.Release();
                }
            }
        }

        private async Task FlushEventsAsync(Event[] events, SummaryState snapshot, Semaphore reply)
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
                SummaryOutput summary = _summarizer.Output(snapshot);
                SummaryEventOutput se = new SummaryEventOutput
                {
                    StartDate = summary.StartDate,
                    EndDate = summary.EndDate,
                    Features = summary.Features
                };
                eventsOut.Add(se);
            }
            if (eventsOut.Count == 0)
            {
                return;
            }
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            var jsonEvents = "";
            try
            {
                jsonEvents = JsonConvert.SerializeObject(eventsOut, Formatting.None);

                await SendEventsAsync(jsonEvents, eventsOut.Count, cts);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Error sending events: {0} waiting 1 second before retrying.",
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
                        Log.ErrorFormat("Error Submitting Events using uri: '{0}' '{1}'",
                            tce, _uri.AbsoluteUri, Util.ExceptionMessage(tce));
                    }
                    else
                    {
                        //Otherwise this was a request timeout.
                        Log.ErrorFormat("Timed out trying to send {0} events after {1}",
                            tce, eventsOut.Count, _config.HttpClientTimeout);
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorFormat("Error Submitting Events using uri: '{0}' '{1}'",
                        ex,
                        _uri.AbsoluteUri,
                         Util.ExceptionMessage(ex));
                }
            }
            if (reply != null)
            {
                reply.Release();
            }
        }

        private async Task SendEventsAsync(String jsonEvents, int count, CancellationTokenSource cts)
        {
            Log.DebugFormat("Submitting {0} events to {1} with json: {2}",
                count, _uri.AbsoluteUri, jsonEvents);

            using (var stringContent = new StringContent(jsonEvents, Encoding.UTF8, "application/json"))
            using (var response = await _httpClient.PostAsync(_uri, stringContent).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Log.ErrorFormat("Error Submitting Events using uri: '{0}'; Status: '{1}'",
                        _uri.AbsoluteUri,
                        response.StatusCode);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Log.Error("Received 401 error, no further events will be posted since SDK key is invalid");
                        _messageQueue.TryAdd(new ShutdownMessage());
                    }
                }
                else
                {
                    Log.DebugFormat("Got {0} when sending events.",
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

    internal interface IEventMessage { }

    internal class EventMessage : IEventMessage
    {
        internal Event Event { get; set; }
    }

    internal class FlushMessage : IEventMessage
    {
        internal Semaphore Reply { get; set; }
    }

    internal class FlushUsersMessage : IEventMessage { }

    internal class ShutdownMessage : IEventMessage { }

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
}
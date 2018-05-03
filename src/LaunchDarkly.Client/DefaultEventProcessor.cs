using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    internal sealed class DefaultEventProcessor : IEventProcessor
    {
        internal static readonly ILog Log = LogManager.GetLogger(typeof(DefaultEventProcessor));
        internal static readonly string CurrentSchemaVersion = "3";

        private readonly BlockingCollection<IEventMessage> _messageQueue;
        private readonly EventDispatcher _dispatcher;
        private readonly Timer _flushTimer;
        private readonly Timer _flushUsersTimer;
        private AtomicBoolean _stopped;
        private AtomicBoolean _inputCapacityExceeded;

        internal DefaultEventProcessor(Configuration config)
        {
            _messageQueue = new BlockingCollection<IEventMessage>(config.EventQueueCapacity);
            _dispatcher = new EventDispatcher(config, _messageQueue);
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
                    ((IDisposable)_dispatcher).Dispose();
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
    
    internal sealed class EventDispatcher : IDisposable
    {
        private static readonly int MaxFlushWorkers = 5;

        private readonly Configuration _config;
        private readonly LRUCacheSet<string> _userKeys;
        private readonly CountdownEvent _flushWorkersCounter;
        private readonly HttpClient _httpClient;
        private readonly Uri _uri;
        private readonly Random _random;
        private long _lastKnownPastTime;
        private volatile bool _disabled;

        internal EventDispatcher(Configuration config, BlockingCollection<IEventMessage> messageQueue)
        {
            _config = config;
            _userKeys = new LRUCacheSet<string>(config.UserKeysCapacity);
            _flushWorkersCounter = new CountdownEvent(1);
            _httpClient = config.HttpClient();
            _uri = new Uri(_config.EventsUri.AbsoluteUri + "bulk");
            _random = new Random();

            _httpClient.DefaultRequestHeaders.Add("X-LaunchDarkly-Event-Schema",
                DefaultEventProcessor.CurrentSchemaVersion);
            
            EventBuffer buffer = new EventBuffer(config.EventQueueCapacity);

            Task.Run(() => RunMainLoop(messageQueue, buffer));
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

        private void RunMainLoop(BlockingCollection<IEventMessage> messageQueue, EventBuffer buffer)
        {
            bool running = true;
            while (running)
            {
                IEventMessage message = messageQueue.Take();
                switch(message)
                {
                    case EventMessage em:
                        ProcessEvent(em.Event, buffer);
                        break;
                    case FlushMessage fm:
                        StartFlush(buffer);
                        break;
                    case FlushUsersMessage fm:
                        _userKeys.Clear();
                        break;
                    case TestSyncMessage tm:
                        WaitForFlushes();
                        tm.Completed();
                        break;
                    case ShutdownMessage sm:
                        WaitForFlushes();
                        running = false;
                        sm.Completed();
                        break;
                }
            }
        }

        private void WaitForFlushes()
        {
            // Our CountdownEvent was initialized with a count of 1, so that's the lowest it can be at this point.
            _flushWorkersCounter.Signal(); // Drop the count to zero if there are no active flush tasks.
            _flushWorkersCounter.Wait();   // Wait until it is zero.
            _flushWorkersCounter.Reset(1);
        }

        private void ProcessEvent(Event e, EventBuffer buffer)
        {
            if (_disabled)
            {
                return;
            }

            // Always record the event in the summarizer.
            buffer.AddToSummary(e);

            // Decide whether to add the event to the payload. Feature events may be added twice, once for
            // the event (if tracked) and once for debugging.
            bool willAddFullEvent = false;
            Event debugEvent = null;
            if (e is FeatureRequestEvent fe)
            {
                if (ShouldSampleEvent())
                {
                    willAddFullEvent = fe.TrackEvents;
                    if (ShouldDebugEvent(fe))
                    {
                        debugEvent = EventFactory.Default.NewDebugEvent(fe);
                    }
                }
            }
            else
            {
                willAddFullEvent = ShouldSampleEvent();
            }

            // For each user we haven't seen before, we add an index event - unless this is already
            // an identify event for that user.
            if (!(willAddFullEvent && _config.InlineUsersInEvents))
            {
                if (e.User != null && !NoticeUser(e.User))
                {
                    if (!(e is IdentifyEvent))
                    {
                        IndexEvent ie = new IndexEvent(e.CreationDate, e.User);
                        buffer.AddEvent(ie);
                    }
                }
            }

            if (willAddFullEvent)
            {
                buffer.AddEvent(e);
            }
            if (debugEvent != null)
            {
                buffer.AddEvent(debugEvent);
            }
        }

        // Adds to the set of users we've noticed, and returns true if the user was already known to us.
        private bool NoticeUser(User user)
        {
            if (user == null || user.Key == null)
            {
                return false;
            }
            return _userKeys.Add(user.Key);
        }

        private bool ShouldDebugEvent(FeatureRequestEvent fe)
        {
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

        private bool ShouldSampleEvent()
        {
            // Sampling interval applies only to fully-tracked events. Note that we don't have to
            // worry about thread-safety of Random here because this method is only executed on a
            // single thread.
            return _config.EventSamplingInterval <= 0 || _random.Next(_config.EventSamplingInterval) == 0;
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

        // Grabs a snapshot of the current internal state, and starts a new task to send it to the server.
        private void StartFlush(EventBuffer buffer)
        {
            if (_disabled)
            {
                return;
            }
            FlushPayload payload = buffer.GetPayload();
            if (payload.Events.Length > 0 || !payload.Summary.Empty)
            {
                lock (_flushWorkersCounter)
                {
                    // Note that this counter will be 1, not 0, when there are no active flush workers.
                    // This is because a .NET CountdownEvent can't be reused without explicitly resetting
                    // it once it has gone to zero.
                    if (_flushWorkersCounter.CurrentCount >= MaxFlushWorkers + 1)
                    {
                        // We already have too many workers, so just leave the events as is
                        return;
                    }
                    // We haven't hit the limit, we'll go ahead and start a flush task
                    _flushWorkersCounter.AddCount(1);
                }
                buffer.Clear();
                Task.Run(() => FlushEventsAsync(payload));
            }
        }

        private async Task FlushEventsAsync(FlushPayload payload)
        {
            EventOutputFormatter formatter = new EventOutputFormatter(_config);
            List<EventOutput> eventsOut = formatter.MakeOutputEvents(payload.Events, payload.Summary);
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            var jsonEvents = JsonConvert.SerializeObject(eventsOut, Formatting.None);
            try
            {
                await SendEventsAsync(jsonEvents, eventsOut.Count, cts);
            }
            catch (Exception e)
            {
                DefaultEventProcessor.Log.DebugFormat("Error sending events: {0}; waiting 1 second before retrying.",
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
                        DefaultEventProcessor.Log.ErrorFormat("Error submitting events using uri: '{0}' '{1}'",
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
                    DefaultEventProcessor.Log.ErrorFormat("Error submitting events using uri: '{0}' '{1}'",
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

            Stopwatch timer = new Stopwatch();
            using (var stringContent = new StringContent(jsonEvents, Encoding.UTF8, "application/json"))
            using (var response = await _httpClient.PostAsync(_uri, stringContent).ConfigureAwait(false))
            {
                timer.Stop();
                DefaultEventProcessor.Log.DebugFormat("Event delivery took {0} ms, response status {1}",
                    response.StatusCode, timer.ElapsedMilliseconds);
                if (response.IsSuccessStatusCode)
                {
                    DateTimeOffset? respDate = response.Headers.Date;
                    if (respDate.HasValue)
                    {
                        Interlocked.Exchange(ref _lastKnownPastTime,
                            Util.GetUnixTimestampMillis(respDate.Value.DateTime));
                    }
                }
                else
                {
                    DefaultEventProcessor.Log.WarnFormat("Unexpected response status when posting events: {0}",
                        response.StatusCode);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        DefaultEventProcessor.Log.Error("Received 401 error, no further events will be posted since SDK key is invalid");
                        _disabled = true;
                    }
                }
            }
        }
    }

    internal sealed class FlushPayload
    {
        internal Event[] Events { get; set; }
        internal EventSummary Summary { get; set; }
    }

    internal sealed class EventBuffer
    {
        private readonly List<Event> _events;
        private readonly EventSummarizer _summarizer;
        private int _capacity;
        private bool _exceededCapacity;

        internal EventBuffer(int capacity)
        {
            _capacity = capacity;
            _events = new List<Event>();
            _summarizer = new EventSummarizer();
        }

        internal void AddEvent(Event e)
        {
            if (_events.Count >= _capacity)
            {
                if (!_exceededCapacity)
                {
                    DefaultEventProcessor.Log.Warn("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
                    _exceededCapacity = true;
                }
            }
            else
            {
                _events.Add(e);
                _exceededCapacity = false;
            }
        }

        internal void AddToSummary(Event e)
        {
            _summarizer.SummarizeEvent(e);
        }

        internal FlushPayload GetPayload()
        {
            return new FlushPayload { Events = _events.ToArray(), Summary = _summarizer.Snapshot() };
        }

        internal void Clear()
        {
            _events.Clear();
            _summarizer.Clear();
        }
    }
}
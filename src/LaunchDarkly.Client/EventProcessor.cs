using System.IO;
using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;

namespace LaunchDarkly.Client
{
    public class EventProcessor : IDisposable, IStoreEvents
    {
        private static readonly ILog Logger = LogProvider.For<EventProcessor>();

        private BlockingCollection<Event> _queue;
        private System.Threading.Timer _timer;

        public EventProcessor(int capacity, TimeSpan frequency)
        {
            _queue = new BlockingCollection<Event>(capacity);
            _timer = new System.Threading.Timer(SubmitEvents, null, frequency, frequency);
        }

        public void Add(Event eventToLog)
        {
            if (!_queue.TryAdd(eventToLog))
                Logger.Warn("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
        }

        public void SubmitEvents(object StateInfo)
        {
            var comsumer = _queue.GetConsumingEnumerable();
            var taken = comsumer.Take<Event>(_queue.BoundedCapacity);

            string json;
            if (taken.Any())
            {
                json = JsonConvert.SerializeObject(taken); 
                File.WriteAllText(Guid.NewGuid() + ".txt", json);
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
        }
    }
}

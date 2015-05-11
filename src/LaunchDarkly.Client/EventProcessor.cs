using System.IO;
using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Collections;
using System.Collections.Generic;

namespace LaunchDarkly.Client
{
    public sealed class EventProcessor : IDisposable, IStoreEvents
    {
        private static readonly ILog Logger = LogProvider.For<EventProcessor>();

        private readonly Configuration _configuration;
        private readonly HttpClient _httpClient;
        private BlockingCollection<Event> _queue;
        private System.Threading.Timer _timer;

        public EventProcessor(Configuration configuration)
        {
            _configuration = configuration;
            _queue = new BlockingCollection<Event>(_configuration.EventQueueCapacity);
            _timer = new System.Threading.Timer(SubmitEvents, null, _configuration.EventQueueFrequency, _configuration.EventQueueFrequency);
            _httpClient = new HttpClient { BaseAddress = _configuration.BaseUri };
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

            if (taken.Any())
            {
                BulkSubmit(taken);
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _timer.Dispose();
            _queue.Dispose();
            _httpClient.Dispose();
        }


        private async void BulkSubmit(IEnumerable<Event> events)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/events/bulk", events);
            
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Error Submitting Events: '{0}'", ex.Message));
            }
        }
    }
}

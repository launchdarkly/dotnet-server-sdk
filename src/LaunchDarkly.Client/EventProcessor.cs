using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    sealed class EventProcessor : IDisposable, IStoreEvents
    {
        private static readonly ILog Logger = LogProvider.For<EventProcessor>();

        private readonly Configuration _config;
        private BlockingCollection<Event> _queue;
        private System.Threading.Timer _timer;
        private readonly HttpClient _httpClient;

        internal EventProcessor(Configuration config)
        {
            _config = config;
            _queue = new BlockingCollection<Event>(_config.EventQueueCapacity);
            _timer = new System.Threading.Timer(SubmitEvents, null, _config.EventQueueFrequency, _config.EventQueueFrequency);
            _httpClient = config.HttpClient;
        }

        private void SubmitEvents(object StateInfo)
        {
            ((IStoreEvents)this).Flush();
        }

        void IStoreEvents.Add(Event eventToLog)
        {
            if (!_queue.TryAdd(eventToLog))
                Logger.Warn("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
        }

        void IDisposable.Dispose()
        {
            ((IStoreEvents) this).Flush();
            _queue.CompleteAdding();
            _timer.Dispose();
            _queue.Dispose();
        }

        void IStoreEvents.Flush()
        {
            Event e;
            List<Event> events = new List<Event>();
            while (_queue.TryTake(out e))
            {
                events.Add(e);
            }

            if (events.Any())
            {
                Task.Run(() => BulkSubmit(events)).GetAwaiter().GetResult();
            }
        }

        private async Task BulkSubmit(IList<Event> events)
        {
            var uri = new Uri(_config.EventsUri.AbsoluteUri + "bulk");
            try
            {
                string json = JsonConvert.SerializeObject(events.ToList(), Formatting.None);
                Logger.Debug("Submitting " + events.Count() + " events to " + uri.AbsoluteUri + " with json: " + json);

                using (var responseTask = _httpClient.PostAsync(uri, new StringContent(json, Encoding.UTF8, "application/json")))
                {
                    HttpResponseMessage response = await responseTask.ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                        Logger.Error(string.Format("Error Submitting Events using uri: '{0}'; Status: '{1}'",
                            uri.AbsoluteUri, response.StatusCode));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Error Submitting Events using uri: '{0}' '{1}'", uri.AbsoluteUri, ex.Message));
            }
        }
    }
}

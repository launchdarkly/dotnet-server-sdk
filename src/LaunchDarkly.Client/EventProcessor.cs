using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal sealed class EventProcessor : IDisposable, IStoreEvents
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<EventProcessor>();

        private readonly Configuration _config;
        private readonly BlockingCollection<Event> _queue;
        private readonly System.Threading.Timer _timer;
        private readonly Uri m_uri;

        internal EventProcessor(Configuration config)
        {
            _config = config;
            _queue = new BlockingCollection<Event>(_config.EventQueueCapacity);
            _timer = new System.Threading.Timer(SubmitEvents, null, _config.EventQueueFrequency,
                _config.EventQueueFrequency);
            m_uri = new Uri(_config.EventsUri.AbsoluteUri + "bulk");
        }

        private void SubmitEvents(object StateInfo)
        {
            ((IStoreEvents) this).Flush();
        }

        void IStoreEvents.Add(Event eventToLog)
        {
            if (!_queue.TryAdd(eventToLog))
                Logger.LogWarning("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
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
                Task.Run(() => BulkSubmitAsync(events)).GetAwaiter().GetResult();
            }
        }

        private async Task BulkSubmitAsync(IList<Event> events)
        {
            try
            {
                var json = JsonConvert.SerializeObject(events.ToList(), Formatting.None);
                var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

                using (var client = _config.HttpClient())
                using (var response = await client.PostAsync(m_uri, stringContent).ConfigureAwait(false))
                {
                    Logger.LogDebug("Submitting " + events.Count + " events to " + m_uri.AbsoluteUri + " with json: " +
                                    json);
                    if (!response.IsSuccessStatusCode)
                        Logger.LogError(string.Format("Error Submitting Events using uri: '{0}'; Status: '{1}'",
                            m_uri.AbsoluteUri, response.StatusCode));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(string.Format("Error Submitting Events using uri: '{0}' '{1}'", m_uri.AbsoluteUri,
                    Util.ExceptionMessage(ex)));
            }
        }
    }
}
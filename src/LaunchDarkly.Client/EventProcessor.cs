using System.IO;
using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using System.Net;

namespace LaunchDarkly.Client
{
    public sealed class EventProcessor : IDisposable, IStoreEvents
    {
        private static readonly ILog Logger = LogProvider.For<EventProcessor>();

        private readonly Configuration _configuration;
        private BlockingCollection<Event> _queue;
        private System.Threading.Timer _timer;

        public EventProcessor(Configuration configuration)
        {
            _configuration = configuration;
            _queue = new BlockingCollection<Event>(_configuration.EventQueueCapacity);
            _timer = new System.Threading.Timer(SubmitEvents, null, _configuration.EventQueueFrequency, _configuration.EventQueueFrequency);
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
                BulkSubmit(json);
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _timer.Dispose();
            _queue.Dispose();
        }


        private void BulkSubmit(string eventsJson)
        {
            try
            {
                var url = new Uri(_configuration.BaseUri + "/api/events/bulk");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.ContentType = "text/json";
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "api_key " + _configuration.ApiKey);

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(eventsJson);
                    streamWriter.Flush();

                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                    if (httpResponse.StatusCode != HttpStatusCode.OK)
                        Logger.Error(string.Format("Error Submitting Events: '{0}'", httpResponse.StatusDescription));
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Error Submitting Events: '{0}'", ex.Message));
            }
        }
    }
}

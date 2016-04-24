using System.IO;
using LaunchDarkly.Client.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Collections.Generic;

namespace LaunchDarkly.Client
{
    public sealed class EventProcessor : IDisposable, IStoreEvents
    {
        private static readonly ILog Logger = LogProvider.For<EventProcessor>();

        private readonly Configuration _config;
        private BlockingCollection<Event> _queue;
        private System.Threading.Timer _timer;

        public EventProcessor(Configuration config)
        {
            _config = config;
            _queue = new BlockingCollection<Event>(_config.EventQueueCapacity);
            _timer = new System.Threading.Timer(SubmitEvents, null, _config.EventQueueFrequency, _config.EventQueueFrequency);
        }

        public void Add(Event eventToLog)
        {
            if (!_queue.TryAdd(eventToLog))
                Logger.Warn("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
        }

        public void SubmitEvents(object StateInfo)
        {
            Flush();
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _timer.Dispose();
            _queue.Dispose();
        }

        public void Flush()
        {
            Event e;
            List<Event> events = new List<Event>();
            while (_queue.TryTake(out e))
            {
                events.Add(e);
            }

            if (events.Any())
            {
                BulkSubmit(events);
            }
        }

        private void BulkSubmit(IEnumerable<Event> events)
        {
            try
            {
                var url = new Uri(_config.BaseUri + "api/events/bulk");

                string json = JsonConvert.SerializeObject(events.ToList());
                Logger.Debug("Submitting " + events.Count() + " events to " + url.AbsoluteUri + " with json: " + json);

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, "api_key " + _config.ApiKey);
                var version = System.Reflection.Assembly.GetAssembly(typeof(LdClient)).GetName().Version;

                httpWebRequest.UserAgent = "DotNetClient/" + version;

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(json);
                    streamWriter.Flush();
                    streamWriter.Close();

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

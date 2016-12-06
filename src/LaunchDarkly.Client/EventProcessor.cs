using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LaunchDarkly.Client
{
    internal sealed class EventProcessor : IDisposable, IStoreEvents
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger<EventProcessor>();

        private readonly Configuration _config;
        private readonly BlockingCollection<Event> _queue;
        private readonly Timer _timer;
        private volatile HttpClient _httpClient;
        private readonly Uri _uri;

        internal EventProcessor(Configuration config)
        {
            _config = config;
            _httpClient = config.HttpClient();
            _queue = new BlockingCollection<Event>(_config.EventQueueCapacity);
            _timer = new Timer(SubmitEvents, null, _config.EventQueueFrequency,
                _config.EventQueueFrequency);
            _uri = new Uri(_config.EventsUri.AbsoluteUri + "bulk");
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
            var cts = new CancellationTokenSource(_config.HttpClientTimeout);
            StringContent stringContent = null;
            try
            {
                var json = JsonConvert.SerializeObject(events.ToList(), Formatting.None);
                stringContent = new StringContent(json, Encoding.UTF8, "application/json");
                Logger.LogDebug("Submitting " + events.Count + " events to " + _uri.AbsoluteUri + " with json: " +
                                stringContent);
                await SendEventsAsync(stringContent, cts);
            }
            catch (Exception e)
            {
                // Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
                _httpClient?.Dispose();
                _httpClient = _config.HttpClient();

                Logger.LogDebug("Error sending events: " + Util.ExceptionMessage(e) +
                                " waiting 1 second before retrying.");
                Thread.Sleep(TimeSpan.FromSeconds(1));
                cts = new CancellationTokenSource(_config.HttpClientTimeout);
                try
                {
                    Logger.LogDebug("Submitting " + events.Count + " events to " + _uri.AbsoluteUri + " with json: " +
                                    stringContent);
                    await SendEventsAsync(stringContent, cts);
                }
                catch (TaskCanceledException tce)
                {
                    if (tce.CancellationToken == cts.Token)
                    {
                        //Indicates the task was cancelled by something other than a request timeout
                        Logger.LogError(string.Format("Error Submitting Events using uri: '{0}' '{1}'", _uri.AbsoluteUri,
                                            Util.ExceptionMessage(tce)) + tce + " " + tce.StackTrace);
                    }
                    else
                    {
                        //Otherwise this was a request timeout.
                        Logger.LogError("Timed out trying to send " + events.Count + " events after " +
                                        _config.HttpClientTimeout);
                    }
                    // Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
                    _httpClient?.Dispose();
                    _httpClient = _config.HttpClient();
                }
                catch (Exception ex)
                {
                    Logger.LogError(string.Format("Error Submitting Events using uri: '{0}' '{1}'", _uri.AbsoluteUri,
                                        Util.ExceptionMessage(ex)) + ex + " " + ex.StackTrace);

                    // Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
                    _httpClient?.Dispose();
                    _httpClient = _config.HttpClient();
                }
            }
        }


        private async Task SendEventsAsync(StringContent content, CancellationTokenSource cts)
        {
            using (var response = await _httpClient.PostAsync(_uri, content).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError(string.Format("Error Submitting Events using uri: '{0}'; Status: '{1}'",
                        _uri.AbsoluteUri, response.StatusCode));
                }
                else
                {
                    Logger.LogDebug("Got " + response.StatusCode + " when sending events.");
                }
            }
        }
    }
}
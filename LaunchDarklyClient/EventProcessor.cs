using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using LaunchDarklyClient.Events;
using LaunchDarklyClient.Interfaces;
using Newtonsoft.Json;

namespace LaunchDarklyClient
{
	internal sealed class EventProcessor : IDisposable, IStoreEvents
	{
		private static readonly ILog log = LogManager.GetLogger<EventProcessor>();

		private readonly Configuration config;
		private readonly BlockingCollection<Event> queue;
		private readonly Timer timer;
		private readonly Uri uri;
		private volatile HttpClient httpClient;

		internal EventProcessor(Configuration config)
		{
			try
			{
				log.Trace($"Start constructor {nameof(EventProcessor)}(Configuration)");

				this.config = config;
				httpClient = config.HttpClient();
				queue = new BlockingCollection<Event>(this.config.EventQueueCapacity);
				timer = new Timer(SubmitEvents, null, this.config.EventQueueFrequency, this.config.EventQueueFrequency);
				uri = new Uri($"{this.config.EventsUri.AbsoluteUri}bulk");
			}
			finally
			{
				log.Trace($"End constructor {nameof(EventProcessor)}(Configuration)");
			}
		}

		void IDisposable.Dispose()
		{
			try
			{
				log.Trace($"Start {nameof(IDisposable.Dispose)}");

				((IStoreEvents) this).Flush();
				queue.CompleteAdding();
				timer.Dispose();
				queue.Dispose();
			}
			finally
			{
				log.Trace($"End {nameof(IDisposable.Dispose)}");
			}
		}

		void IStoreEvents.Add(Event eventToLog)
		{
			try
			{
				log.Trace($"Start {nameof(IStoreEvents.Add)}");

				if (!queue.TryAdd(eventToLog))
				{
					log.Warn("Exceeded event queue capacity. Increase capacity to avoid dropping events.");
				}
			}
			finally
			{
				log.Trace($"End {nameof(IStoreEvents.Add)}");
			}
		}

		void IStoreEvents.Flush()
		{
			try
			{
				log.Trace($"Start {nameof(IStoreEvents.Flush)}");

				Event e;
				List<Event> events = new List<Event>();
				while (queue.TryTake(out e))
				{
					events.Add(e);
				}

				if (events.Any())
				{
					Task.Run(() => BulkSubmitAsync(events)).GetAwaiter().GetResult();
				}
			}
			finally
			{
				log.Trace($"End {nameof(IStoreEvents.Flush)}");
			}
		}

		private void SubmitEvents(object stateInfo)
		{
			try
			{
				log.Trace($"Start {nameof(SubmitEvents)}");

				((IStoreEvents) this).Flush();
			}
			finally
			{
				log.Trace($"End {nameof(SubmitEvents)}");
			}
		}

		private async Task BulkSubmitAsync(IList<Event> events)
		{
			try
			{
				log.Trace($"Start {nameof(BulkSubmitAsync)}");

				CancellationTokenSource cts = new CancellationTokenSource(config.HttpClientTimeout);
				string jsonEvents = "";
				try
				{
					jsonEvents = JsonConvert.SerializeObject(events.ToList(), Formatting.None);
					log.Debug($"Submitting {events.Count} events to {uri.AbsoluteUri} with json: {jsonEvents}");
					await SendEventsAsync(jsonEvents, cts);
				}
				catch (Exception e)
				{
					// Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
					httpClient?.Dispose();
					httpClient = config.HttpClient();

					log.Debug($"Error sending events: {Util.ExceptionMessage(e)} waiting 1 second before retrying.");
					Task.Delay(TimeSpan.FromSeconds(1)).Wait();
					cts = new CancellationTokenSource(config.HttpClientTimeout);
					try
					{
						log.Debug($"Submitting {events.Count} events to {uri.AbsoluteUri} with json: {jsonEvents}");
						await SendEventsAsync(jsonEvents, cts);
					}
					catch (TaskCanceledException tce)
					{
						log.Error(tce.CancellationToken == cts.Token ? $"Error Submitting Events using uri: \'{uri.AbsoluteUri}\' \'{Util.ExceptionMessage(tce)}\'{tce} {tce.StackTrace}" : $"Timed out trying to send {events.Count} events after {config.HttpClientTimeout}");
						// Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
						httpClient?.Dispose();
						httpClient = config.HttpClient();
					}
					catch (Exception ex)
					{
						log.Error($"Error Submitting Events using uri: \'{uri.AbsoluteUri}\' \'{Util.ExceptionMessage(ex)}\'{ex} {ex.StackTrace}");

						// Using a new client after errors because: https://github.com/dotnet/corefx/issues/11224
						httpClient?.Dispose();
						httpClient = config.HttpClient();
					}
				}
			}
			finally
			{
				log.Trace($"End {nameof(BulkSubmitAsync)}");
			}
		}

		private async Task SendEventsAsync(string jsonEvents, CancellationTokenSource cts)
		{
			try
			{
				log.Trace($"Start {nameof(SendEventsAsync)}");

				using (StringContent stringContent = new StringContent(jsonEvents, Encoding.UTF8, "application/json"))
				{
					using (HttpResponseMessage response = await httpClient.PostAsync(uri, stringContent).ConfigureAwait(false))
					{
						if (!response.IsSuccessStatusCode)
						{
							log.Error($"Error Submitting Events using uri: '{uri.AbsoluteUri}'; Status: '{response.StatusCode}'");
						}
						else
						{
							log.Debug($"Got {response.StatusCode} when sending events.");
						}
					}
				}
			}
			finally
			{
				log.Trace($"End {nameof(SendEventsAsync)}");
			}
		}
	}
}
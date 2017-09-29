using System;
using System.Net.Http;
using System.Reflection;
using Common.Logging;
using LaunchDarklyClient.Interfaces;

namespace LaunchDarklyClient
{
	public class Configuration
	{
		private static readonly ILog log = LogManager.GetLogger<Configuration>();

		private const int DefaultEventQueueCapacity = 500;
		public static TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(1);

		internal static readonly string Version = typeof(LdClient).GetTypeInfo().Assembly.GetName().Version.ToString();

		internal static readonly Uri DefaultUri = new Uri("https://app.launchdarkly.com");
		private static readonly Uri defaultEventsUri = new Uri("https://events.launchdarkly.com");
		private static readonly TimeSpan defaultEventQueueFrequency = TimeSpan.FromSeconds(5);
		private static readonly TimeSpan defaultStartWaitTime = TimeSpan.FromSeconds(10);
		private static readonly TimeSpan defaultHttpClientTimeout = TimeSpan.FromSeconds(10);
		public Uri BaseUri {get; internal set;}
		public Uri EventsUri {get; internal set;}
		public string SdkKey {get; internal set;}
		public int EventQueueCapacity {get; internal set;}
		public TimeSpan EventQueueFrequency {get; internal set;}
		public TimeSpan PollingInterval {get; internal set;}
		public TimeSpan StartWaitTime {get; internal set;}
		public TimeSpan HttpClientTimeout {get; internal set;}
		public HttpClientHandler HttpClientHandler {get; internal set;}
		public bool Offline {get; internal set;}
		internal IFeatureStore FeatureStore {get; set;}

	public static Configuration Default(string sdkKey)
		{
			try
			{
				log.Trace($"Start constructor {nameof(Default)}(string)");

				Configuration defaultConfiguration = new Configuration
				{
					BaseUri = DefaultUri,
					EventsUri = defaultEventsUri,
					EventQueueCapacity = DefaultEventQueueCapacity,
					EventQueueFrequency = defaultEventQueueFrequency,
					PollingInterval = DefaultPollingInterval,
					StartWaitTime = defaultStartWaitTime,
					HttpClientTimeout = defaultHttpClientTimeout,
					HttpClientHandler = new HttpClientHandler(),
					Offline = false,
					SdkKey = sdkKey,
					FeatureStore = new InMemoryFeatureStore()
				};

				return defaultConfiguration;
			}
			finally
			{
				log.Trace($"End constructor {nameof(Default)}(string)");
			}
		}

		internal HttpClient HttpClient()
		{
			try
			{
				log.Trace($"Start {nameof(HttpClient)}");

				HttpClient httpClient = new HttpClient(HttpClientHandler, false);
				httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetClient/" + Version);
				httpClient.DefaultRequestHeaders.Add("Authorization", SdkKey);
				return httpClient;
			}
			finally
			{
				log.Trace($"End {nameof(HttpClient)}");
			}
		}
	}
}
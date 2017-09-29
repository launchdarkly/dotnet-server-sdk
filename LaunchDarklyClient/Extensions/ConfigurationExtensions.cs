using System;
using System.Net.Http;
using Common.Logging;
using LaunchDarklyClient.Interfaces;

namespace LaunchDarklyClient.Extensions
{
	public static class ConfigurationExtensions
	{
		private static readonly ILog log = LogManager.GetLogger(nameof(ConfigurationExtensions));

		public static Configuration WithUri(this Configuration configuration, string uri)
		{
			try
			{
				log.Trace($"Start {nameof(WithUri)}");

				if (uri != null)
				{
					configuration.BaseUri = new Uri(uri);
				}

				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithUri)}");
			}
		}

		public static Configuration WithUri(this Configuration configuration, Uri uri)
		{
			try
			{
				log.Trace($"Start {nameof(WithUri)}");

				if (uri != null)
				{
					configuration.BaseUri = uri;
				}

				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithUri)}");
			}
		}

		public static Configuration WithEventsUri(this Configuration configuration, string uri)
		{
			try
			{
				log.Trace($"Start {nameof(WithEventsUri)}");

				if (uri != null)
				{
					configuration.EventsUri = new Uri(uri);
				}

				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithEventsUri)}");
			}
		}

		public static Configuration WithEventsUri(this Configuration configuration, Uri uri)
		{
			try
			{
				log.Trace($"Start {nameof(WithEventsUri)}");

				if (uri != null)
				{
					configuration.EventsUri = uri;
				}

				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithEventsUri)}");
			}
		}

		public static Configuration WithEventQueueCapacity(this Configuration configuration, int eventQueueCapacity)
		{
			try
			{
				log.Trace($"Start {nameof(WithEventQueueCapacity)}");

				configuration.EventQueueCapacity = eventQueueCapacity;
				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithEventQueueCapacity)}");
			}
		}

		internal static Configuration WithEventQueueCapacity(this Configuration configuration, string eventQueueCapacity)
		{
			try
			{
				log.Trace($"Start {nameof(WithEventQueueCapacity)}");

				return eventQueueCapacity != null ? WithEventQueueCapacity(configuration, int.Parse(eventQueueCapacity)) : configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithEventQueueCapacity)}");
			}
		}

		public static Configuration WithEventQueueFrequency(this Configuration configuration, TimeSpan frequency)
		{
			try
			{
				log.Trace($"Start {nameof(WithEventQueueFrequency)}");

				configuration.EventQueueFrequency = frequency;

				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithEventQueueFrequency)}");
			}
		}

		internal static Configuration WithEventQueueFrequency(this Configuration configuration, string frequency)
		{
			try
			{
				log.Trace($"Start {nameof(WithEventQueueFrequency)}");

				return frequency != null ? WithEventQueueFrequency(configuration, TimeSpan.FromSeconds(int.Parse(frequency))) : configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithEventQueueFrequency)}");
			}
		}

		public static Configuration WithPollingInterval(this Configuration configuration, TimeSpan pollingInterval)
		{
			try
			{
				log.Trace($"Start {nameof(WithPollingInterval)}");

				configuration.PollingInterval = pollingInterval.CompareTo(Configuration.DefaultPollingInterval) < 0 ? Configuration.DefaultPollingInterval : pollingInterval;
				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithPollingInterval)}");
			}
		}

		public static Configuration WithStartWaitTime(this Configuration configuration, TimeSpan startWaitTime)
		{
			try
			{
				log.Trace($"Start {nameof(WithStartWaitTime)}");

				configuration.StartWaitTime = startWaitTime;
				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithStartWaitTime)}");
			}
		}

		public static Configuration WithOffline(this Configuration configuration, bool offline)
		{
			try
			{
				log.Trace($"Start {nameof(WithOffline)}");

				configuration.Offline = offline;
				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithOffline)}");
			}
		}

		public static Configuration WithLoggerFactory(this Configuration configuration, ILogManager logManager)
		{
			try
			{
				log.Trace($"Start {nameof(WithLoggerFactory)}");

				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithLoggerFactory)}");
			}
		}

		public static Configuration WithHttpClientTimeout(this Configuration configuration, TimeSpan timeSpan)
		{
			try
			{
				log.Trace($"Start {nameof(WithHttpClientTimeout)}");

				configuration.HttpClientTimeout = timeSpan;
				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithHttpClientTimeout)}");
			}
		}

		public static Configuration WithFeatureStore(this Configuration configuration, IFeatureStore featureStore)
		{
			try
			{
				log.Trace($"Start {nameof(WithFeatureStore)}");

				if (featureStore != null)
				{
					configuration.FeatureStore = featureStore;
				}
				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithFeatureStore)}");
			}
		}

		public static Configuration WithHttpClientHandler(this Configuration configuration, HttpClientHandler httpClientHandler)
		{
			try
			{
				log.Trace($"Start {nameof(WithHttpClientHandler)}");

				configuration.HttpClientHandler = httpClientHandler;
				return configuration;
			}
			finally
			{
				log.Trace($"End {nameof(WithHttpClientHandler)}");
			}
		}
	}
}
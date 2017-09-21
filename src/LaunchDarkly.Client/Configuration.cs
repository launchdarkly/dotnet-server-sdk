using System;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace LaunchDarkly.Client
{
    public class Configuration
    {
        public Uri BaseUri { get; internal set; }
        public Uri StreamUri { get; internal set; }
        public Uri EventsUri { get; internal set; }
        public string SdkKey { get; internal set; }
        public bool Stream { get; internal set; }
        public int EventQueueCapacity { get; internal set; }
        public TimeSpan EventQueueFrequency { get; internal set; }
        public TimeSpan PollingInterval { get; internal set; }
        public TimeSpan StartWaitTime { get; internal set; }
        // The time out when reading data from the EventSource API. If null, defaults to 5 minutes.
        public TimeSpan ReadTimeout { get; internal set; }
        // The time to wait before attempting to reconnect to the EventSource API. If null, defaults to 1 second.
        public TimeSpan ReconnectTime { get; internal set; }
        // The connection time out. If null, defaults to 10 seconds.
        public TimeSpan ConnectionTimeout { get; internal set; }
        public TimeSpan HttpClientTimeout { get; internal set; }
        public HttpClientHandler HttpClientHandler { get; internal set; }
        public bool UseLdd { get; internal set; }
        public bool Offline { get; internal set; }
        internal IFeatureStore FeatureStore { get; set; }


        internal static readonly string Version = ((AssemblyInformationalVersionAttribute) typeof(LdClient)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))
            .InformationalVersion;

        public static TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(1);
        internal static readonly Uri DefaultUri = new Uri("https://app.launchdarkly.com");
        private static readonly Uri DefaultStreamUri = new Uri("https://stream.launchdarkly.com");
        private static readonly Uri DefaultEventsUri = new Uri("https://events.launchdarkly.com");
        private static readonly int DefaultEventQueueCapacity = 500;
        private static readonly TimeSpan DefaultEventQueueFrequency = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DefaultStartWaitTime = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultReconnectTime = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultHttpClientTimeout = TimeSpan.FromSeconds(10);

        public static Configuration Default(string sdkKey)
        {
            var defaultConfiguration = new Configuration
            {
                BaseUri = DefaultUri,
                StreamUri = DefaultStreamUri,
                EventsUri = DefaultEventsUri,
                EventQueueCapacity = DefaultEventQueueCapacity,
                EventQueueFrequency = DefaultEventQueueFrequency,
                PollingInterval = DefaultPollingInterval,
                StartWaitTime = DefaultStartWaitTime,
                ReadTimeout = DefaultReadTimeout,
                ReconnectTime = DefaultReconnectTime,
                ConnectionTimeout = DefaultConnectionTimeout,
                HttpClientTimeout = DefaultHttpClientTimeout,
                HttpClientHandler = new HttpClientHandler(),
                Offline = false,
                SdkKey = sdkKey,
                FeatureStore = new InMemoryFeatureStore(),
                Stream = true
            };

            return defaultConfiguration;
        }

        internal HttpClient HttpClient()
        {
            var httpClient = new HttpClient(handler: HttpClientHandler, disposeHandler: false);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetClient/" + Version);
            httpClient.DefaultRequestHeaders.Add("Authorization", SdkKey);
            return httpClient;
        }
    }

    public static class ConfigurationExtensions
    {
        public static Configuration WithUri(this Configuration configuration, string uri)
        {
            if (uri != null)
                configuration.BaseUri = new Uri(uri);

            return configuration;
        }

        public static Configuration WithUri(this Configuration configuration, Uri uri)
        {
            if (uri != null)
                configuration.BaseUri = uri;

            return configuration;
        }

        public static Configuration WithStreamUri(this Configuration configuration, string uri)
        {
            if (uri != null)
                configuration.StreamUri = new Uri(uri);

            return configuration;
        }

        public static Configuration WithStreamUri(this Configuration configuration, Uri uri)
        {
            if (uri != null)
                configuration.StreamUri = uri;

            return configuration;
        }

        public static Configuration WithEventsUri(this Configuration configuration, string uri)
        {
            if (uri != null)
                configuration.EventsUri = new Uri(uri);

            return configuration;
        }

        public static Configuration WithEventsUri(this Configuration configuration, Uri uri)
        {
            if (uri != null)
                configuration.EventsUri = uri;

            return configuration;
        }

        public static Configuration WithEventQueueCapacity(this Configuration configuration, int eventQueueCapacity)
        {
            configuration.EventQueueCapacity = eventQueueCapacity;
            return configuration;
        }

        internal static Configuration WithEventQueueCapacity(this Configuration configuration, string eventQueueCapacity)
        {
            if (eventQueueCapacity != null)
                return WithEventQueueCapacity(configuration, int.Parse(eventQueueCapacity));

            return configuration;
        }

        public static Configuration WithEventQueueFrequency(this Configuration configuration, TimeSpan frequency)
        {
            configuration.EventQueueFrequency = frequency;

            return configuration;
        }

        internal static Configuration WithEventQueueFrequency(this Configuration configuration, string frequency)
        {
            if (frequency != null)
                return WithEventQueueFrequency(configuration, TimeSpan.FromSeconds(int.Parse(frequency)));

            return configuration;
        }

        public static Configuration WithPollingInterval(this Configuration configuration, TimeSpan pollingInterval)
        {
            if (pollingInterval.CompareTo(Configuration.DefaultPollingInterval) < 0)
            {
                configuration.PollingInterval = Configuration.DefaultPollingInterval;
            }
            else
            {
                configuration.PollingInterval = pollingInterval;
            }
            return configuration;
        }

        public static Configuration WithStartWaitTime(this Configuration configuration, TimeSpan startWaitTime)
        {
            configuration.StartWaitTime = startWaitTime;
            return configuration;
        }

        public static Configuration WithUseLdd(this Configuration configuration, bool useLdd)
        {
            configuration.UseLdd = useLdd;
            return configuration;
        }

        public static Configuration WithOffline(this Configuration configuration, bool offline)
        {
            configuration.Offline = offline;
            return configuration;
        }

        public static Configuration WithLoggerFactory(this Configuration configuration, ILoggerFactory loggerFactory)
        {
            if (loggerFactory != null)
                LdLogger.LoggerFactory = loggerFactory;

            return configuration;
        }

        public static Configuration WithHttpClientTimeout(this Configuration configuration, TimeSpan timeSpan)
        {
            configuration.HttpClientTimeout = timeSpan;
            return configuration;
        }


        public static Configuration WithReadTimeout(this Configuration configuration, TimeSpan timeSpan)
        {
            configuration.ReadTimeout = timeSpan;
            return configuration;
        }

        public static Configuration WithReconnectTime(this Configuration configuration, TimeSpan timeSpan)
        {
            configuration.ReconnectTime = timeSpan;
            return configuration;
        }
        public static Configuration WithConnectionTimeout(this Configuration configuration, TimeSpan timeSpan)
        {
            configuration.ConnectionTimeout = timeSpan;
            return configuration;
        }

        public static Configuration WithFeatureStore(this Configuration configuration, IFeatureStore featureStore)
        {
            if (featureStore != null)
            {
                configuration.FeatureStore = featureStore;
            }
            return configuration;
        }

        public static Configuration WithHttpClientHandler(this Configuration configuration, HttpClientHandler httpClientHandler)
        {
            configuration.HttpClientHandler = httpClientHandler;
            return configuration;
        }

        public static Configuration WithStream(this Configuration configuration, bool stream)
        {
            configuration.Stream = stream;
            return configuration;
        }
    }
}
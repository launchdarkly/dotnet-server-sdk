using System;
using System.Collections;
using System.Configuration;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;

namespace LaunchDarkly.Client
{
    public class Configuration
    {
        public Uri BaseUri { get; internal set; }
        public Uri EventsUri { get; internal set; }
        public string SdkKey { get; internal set; }
        public int EventQueueCapacity { get; internal set; }
        public TimeSpan EventQueueFrequency { get; internal set; }
        public TimeSpan PollingInterval { get; internal set; }
        public TimeSpan StartWaitTime { get; internal set; }
        public bool Offline { get; internal set; }
        public HttpClient HttpClient
        {
            get
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetClient/" + Version);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(SdkKey);
                _httpClient.Timeout = TimeSpan.FromSeconds(10);
                return _httpClient;
            }
            internal set { _httpClient = value; }
        }


        public static TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(1);
        private static readonly Uri DefaultUri = new Uri("https://app.launchdarkly.com");
        private static readonly Uri DefaultEventsUri = new Uri("https://events.launchdarkly.com");
        private static readonly int DefaultEventQueueCapacity = 500;
        private static readonly TimeSpan DefaultEventQueueFrequency = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DefaultStartWaitTime = TimeSpan.FromSeconds(5);
        private HttpClient _httpClient;
        private static readonly Version Version = Assembly.GetAssembly(typeof(LdClient)).GetName().Version;

        private Configuration() { }

        public static Configuration Default()
        {
            var defaultConfiguration = new Configuration
            {
                BaseUri = DefaultUri,
                EventsUri = DefaultEventsUri,
                EventQueueCapacity = DefaultEventQueueCapacity,
                EventQueueFrequency = DefaultEventQueueFrequency,
                PollingInterval = DefaultPollingInterval,
                StartWaitTime = DefaultStartWaitTime,
                Offline = false,
                _httpClient = new HttpClient(new WebRequestHandler()
                {
                    // RequestCacheLevel.Revalidate enables proper Etag caching
                    CachePolicy = new RequestCachePolicy(RequestCacheLevel.Revalidate)
                })


            };
            return defaultConfiguration;
        }

        private static Configuration OverwriteFromFile(Configuration defaultConfiguration)
        {
            var configSection = (IDictionary)ConfigurationManager.GetSection("LaunchDarkly");
            if (configSection == null) return defaultConfiguration;

            return defaultConfiguration
                                    .WithUri((string)configSection["BaseUri"])
                                    .WithSdkKey((string)configSection["SdkKey"])
                                    .WithEventQueueCapacity((string)configSection["EventQueueCapacity"])
                                    .WithEventQueueFrequency((string)configSection["EventQueueFrequency"]);
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

        public static Configuration WithSdkKey(this Configuration configuration, string sdkKey)
        {
            if (sdkKey != null)
                configuration.SdkKey = sdkKey;

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

        public static Configuration WithOffline(this Configuration configuration, bool offline)
        {
            configuration.Offline = offline;
            return configuration;
        }

        public static Configuration WithHttpClient(this Configuration configuration, HttpClient httpClient)
        {
            if (httpClient != null)
                configuration.HttpClient = httpClient;

            return configuration;
        }

    }
}

using System;
using System.Collections;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;

namespace LaunchDarkly.Client
{
    public class Configuration
    {
        public Uri BaseUri { get; internal set; }
        public string ApiKey { get; internal set; }
        public int EventQueueCapacity { get; internal set; }
        public TimeSpan EventQueueFrequency { get; internal set; }
        public TimeSpan PollingInterval { get; internal set; }
        public HttpClient HttpClient
        {
            get
            {
                var version = System.Reflection.Assembly.GetAssembly(typeof(LdClient)).GetName().Version;
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetClient/" + version);
                _httpClient.BaseAddress = BaseUri;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("api_key", ApiKey);
                return _httpClient;
            }
            internal set { _httpClient = value; }
        }

        public static TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(1);
        private static Uri DefaultUri = new Uri("https://app.launchdarkly.com");
        private static int DefaultEventQueueCapacity = 500;
        private static TimeSpan DefaultEventQueueFrequency = TimeSpan.FromSeconds(2);
        private HttpClient _httpClient;
        private Configuration() { }

        public static Configuration Default()
        {
            var defaultConfiguration = new Configuration
            {
                BaseUri = DefaultUri,
                EventQueueCapacity = DefaultEventQueueCapacity,
                EventQueueFrequency = DefaultEventQueueFrequency,
                PollingInterval = DefaultPollingInterval,
                _httpClient = new HttpClient(new WebRequestHandler()
                {
                    // RequestCacheLevel.Revalidate enables proper Etag caching
                    CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate)
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
                                    .WithApiKey((string)configSection["ApiKey"])
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

        public static Configuration WithApiKey(this Configuration configuration, string apiKey)
        {
            if (apiKey != null)
                configuration.ApiKey = apiKey;

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
                return WithEventQueueCapacity(configuration, Int32.Parse(eventQueueCapacity));

            return configuration;
        }

        public static Configuration WithEventQueueFrequency(this Configuration configuration, TimeSpan frequency)
        {
            if (frequency != null)
                configuration.EventQueueFrequency = frequency;

            return configuration;
        }

        internal static Configuration WithEventQueueFrequency(this Configuration configuration, string frequency)
        {
            if (frequency != null)
                return WithEventQueueFrequency(configuration, TimeSpan.FromSeconds(Int32.Parse(frequency)));

            return configuration;
        }

        public static Configuration WithPollingInterval(this Configuration configuration, TimeSpan pollingInterval)
        {
            if (pollingInterval != null)
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

        public static Configuration WithHttpClient(this Configuration configuration, HttpClient httpClient)
        {
            if (httpClient != null)
                configuration.HttpClient = httpClient;

            return configuration;
        }
    }
}

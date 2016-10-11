using System;
using System.Collections;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;


namespace LaunchDarkly.Client
{
    public class Configuration
    {
        public virtual Uri BaseUri { get; internal set; }
        public Uri EventsUri { get; internal set; }
        public virtual string SdkKey { get; internal set; }
        public int EventQueueCapacity { get; internal set; }
        public TimeSpan EventQueueFrequency { get; internal set; }
        public TimeSpan PollingInterval { get; internal set; }
        public TimeSpan StartWaitTime { get; internal set; }
        public bool Offline { get; internal set; }
        public static TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(1);

        internal static readonly string Version = ((AssemblyInformationalVersionAttribute) typeof(LdClient)
                .GetTypeInfo()
                .Assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute)))
            .InformationalVersion;

        internal static readonly Uri DefaultUri = new Uri("https://app.launchdarkly.com");
        private static readonly Uri DefaultEventsUri = new Uri("https://events.launchdarkly.com");
        private static readonly int DefaultEventQueueCapacity = 500;
        private static readonly TimeSpan DefaultEventQueueFrequency = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DefaultStartWaitTime = TimeSpan.FromSeconds(5);

        public static Configuration Default(string sdkKey)
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
                SdkKey = sdkKey
            };

            return defaultConfiguration;
        }

        private static Configuration OverwriteFromFile(Configuration defaultConfiguration)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            var configSection = (IDictionary) config.GetSection("LaunchDarkly");
            if (configSection == null) return defaultConfiguration;

            return defaultConfiguration
                .WithUri((string) configSection["BaseUri"])
                .WithSdkKey((string) configSection["SdkKey"])
                .WithEventQueueCapacity((string) configSection["EventQueueCapacity"])
                .WithEventQueueFrequency((string) configSection["EventQueueFrequency"]);
        }

        // virtual so we can mock it in tests.
        // Not using singleton client due to: https://github.com/dotnet/corefx/issues/11224
        internal virtual HttpClient HttpClient()
        {
            var httpClient = new HttpClient(new HttpClientHandler());
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetClient/" + Version);
            httpClient.DefaultRequestHeaders.Add("Authorization", SdkKey);
            //This is the request timeout
            httpClient.Timeout = TimeSpan.FromSeconds(10);
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

        internal static Configuration WithSdkKey(this Configuration configuration, string sdkKey)
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
            if (startWaitTime != null)
                configuration.StartWaitTime = startWaitTime;

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
    }
}
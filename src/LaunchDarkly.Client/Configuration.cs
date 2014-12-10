using System;
using System.Collections;
using System.Configuration;

namespace LaunchDarkly.Client
{

    public class Configuration
    { 
        public Uri BaseUri { get; internal set; }
        public string ApiKey { get; internal set; }
        public int EventQueueCapacity { get; internal set; }
        public TimeSpan EventQueueFrequency { get; internal set; }

        private static Uri DefaultUri = new Uri("https://app.launchdarkly.com");
        private static int DefaultEventQueueCapacity = 500;
        private static TimeSpan DefaultEventQueueFrequency = TimeSpan.FromSeconds(30); // In Seconds

        private Configuration() { }

        public static Configuration Default()
        {
            var defaultConfiguration = new Configuration 
                                {   
                                    BaseUri = DefaultUri,
                                    EventQueueCapacity = DefaultEventQueueCapacity,
                                    EventQueueFrequency = DefaultEventQueueFrequency
                                };

            return OverwriteFromFile(defaultConfiguration);
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
            if(uri != null)
                configuration.BaseUri = uri;

            return configuration;
        }

        public static Configuration WithApiKey(this Configuration configuration, string apiKey)
        {   
            if(apiKey != null)
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
            configuration.EventQueueFrequency = frequency;
            return configuration;
        }

        internal static Configuration WithEventQueueFrequency(this Configuration configuration, string frequency)
        {
            if (frequency != null)
                return WithEventQueueFrequency(configuration, TimeSpan.FromSeconds(Int32.Parse(frequency)));

            return configuration;
        }
    }
}

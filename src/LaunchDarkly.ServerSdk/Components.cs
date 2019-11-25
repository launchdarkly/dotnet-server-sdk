using Common.Logging;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// Provides factories for the standard implementations of LaunchDarkly component interfaces.
    /// </summary>
    public static class Components
    {
        private static IDataStoreFactory _inMemoryDataStoreFactory = new InMemoryDataStoreFactory();
        private static IEventProcessorFactory _eventProcessorFactory = new DefaultEventProcessorFactory();
        private static IEventProcessorFactory _nullEventProcessorFactory = new NullEventProcessorFactory();
        private static IDataSourceFactory _dataSourceFactory = new DefaultDataSourceFactory();
        private static IDataSourceFactory _nullDataSourceFactory = new NullDataSourceFactory();
        
        /// <summary>
        /// Returns a factory for the default in-memory implementation of <see cref="IDataStore"/>.
        /// </summary>
        public static IDataStoreFactory InMemoryDataStore
        {
            get
            {
                return _inMemoryDataStoreFactory;
            }
        }

        /// <summary>
        /// Returns a factory for the default implementation of <see cref="IEventProcessor"/>, which
        /// forwards all analytics events to LaunchDarkly (unless the client is offline).
        /// </summary>
        public static IEventProcessorFactory DefaultEventProcessor
        {
            get
            {
                return _eventProcessorFactory;
            }
        }

        /// <summary>
        /// Returns a factory for a null implementation of <see cref="IEventProcessor"/>, which will
        /// discard all analytics events and not send them to LaunchDarkly, regardless of any
        /// other configuration.
        /// </summary>
        public static IEventProcessorFactory NullEventProcessor
        {
            get
            {
                return _nullEventProcessorFactory;
            }
        }

        /// <summary>
        /// Returns a factory for the default implementation of <see cref="IDataSource"/>, which
        /// receives feature flag data from LaunchDarkly using either streaming or polling as configured
        /// (or does nothing if the client is offline, or in LDD mode).
        /// </summary>
        public static IDataSourceFactory DefaultDataSource
        {
            get
            {
                return _dataSourceFactory;
            }
        }

        /// <summary>
        /// Returns a factory for a null implementation of <see cref="IDataSource"/>, which
        /// does not connect to LaunchDarkly, regardless of any other configuration.
        /// </summary>
        public static IDataSourceFactory NullDataSource
        {
            get
            {
                return _nullDataSourceFactory;
            }
        }
    }

    internal class DefaultEventProcessorFactory : IEventProcessorFactory
    {
        private const string EventsUriPath = "bulk";

        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config)
        {
            if (config.Offline)
            {
                return new NullEventProcessor();
            }
            else
            {
                return new DefaultEventProcessor(config.EventProcessorConfiguration,
                    new DefaultUserDeduplicator(config),
                    Util.MakeHttpClient(config.HttpRequestConfiguration, ServerSideClientEnvironment.Instance),
                    EventsUriPath);
            }
        }
    }

    internal class NullEventProcessorFactory : IEventProcessorFactory
    {
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config)
        {
            return new NullEventProcessor();
        }
    }

    internal class InMemoryDataStoreFactory : IDataStoreFactory
    {
        IDataStore IDataStoreFactory.CreateDataStore()
        {
            return new InMemoryDataStore();
        }
    }

    internal class DefaultDataSourceFactory : IDataSourceFactory
    {
        // Note, logger uses LDClient class name for backward compatibility
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        IDataSource IDataSourceFactory.CreateDataSource(Configuration config, IDataStore dataStore)
        {
            if (config.Offline)
            {
                Log.Info("Starting Launchdarkly client in offline mode.");
                return new NullDataSource();
            }
            else if (config.UseLdd)
            {
                Log.Info("Starting LaunchDarkly in LDD mode. Skipping direct feature retrieval.");
                return new NullDataSource();
            }
            else
            {
                FeatureRequestor requestor = new FeatureRequestor(config);
                if (config.IsStreamingEnabled)
                {
                    return new StreamProcessor(config, requestor, dataStore, null);
                }
                else
                {
                    Log.Warn("You should only disable the streaming API if instructed to do so by LaunchDarkly support");
                    return new PollingProcessor(config, requestor, dataStore);
                }
            }
        }
    }

    internal class NullDataSourceFactory : IDataSourceFactory
    {
        IDataSource IDataSourceFactory.CreateDataSource(Configuration config, IDataStore dataStore)
        {
            return new NullDataSource();
        }
    }
}

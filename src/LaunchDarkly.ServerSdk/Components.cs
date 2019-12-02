using Common.Logging;
using LaunchDarkly.Sdk.Interfaces;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Helpers;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataSources;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Events;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// Provides factories for the standard implementations of LaunchDarkly component interfaces.
    /// </summary>
    public static class Components
    {
        private static readonly IDataStoreFactory _inMemoryDataStoreFactory = new InMemoryDataStoreFactory();
        private static readonly IEventProcessorFactory _eventProcessorFactory = new DefaultEventProcessorFactory();
        private static readonly IEventProcessorFactory _nullEventProcessorFactory = new NullEventProcessorFactory();
        private static readonly IDataSourceFactory _dataSourceFactory = new DefaultDataSourceFactory();
        private static readonly IDataSourceFactory _nullDataSourceFactory = new NullDataSourceFactory();
        
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

        public static PersistentDataStoreFactory PersistentStore(IPersistentDataStoreFactory storeFactory)
        {
            return new PersistentDataStoreFactory(storeFactory);
        }

        public static PersistentDataStoreFactory PersistentStore(IPersistentDataStoreAsyncFactory storeFactory)
        {
            return new PersistentDataStoreFactory(storeFactory);
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

    internal class DefaultEventProcessorFactory : IEventProcessorFactoryWithDiagnostics
    {
        IEventProcessor IEventProcessorFactory.CreateEventProcessor(Configuration config)
        {
            return CreateEventProcessor(config, null);
        }

        public IEventProcessor CreateEventProcessor(Configuration config, IDiagnosticStore diagnosticStore) {
            if (config.Offline)
            {
                return new NullEventProcessor();
            }
            else
            {
                return new DefaultEventProcessor(config.EventProcessorConfiguration,
                    new DefaultUserDeduplicator(config),
                    Util.MakeHttpClient(config.HttpRequestConfiguration, ServerSideClientEnvironment.Instance),
                    diagnosticStore, null, null);
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

    internal class DefaultDataSourceFactory : IDataSourceFactoryWithDiagnostics
    {
        // Note, logger uses LDClient class name for backward compatibility
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        public IDataSource CreateDataSource(Configuration config, IDataStore dataStore)
        {
            return CreateDataSource(config, dataStore, null);
        }

        public IDataSource CreateDataSource(Configuration config, IDataStore dataStore, IDiagnosticStore diagnosticStore)
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
                    return new StreamProcessor(config, requestor, dataStore, null, diagnosticStore);
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

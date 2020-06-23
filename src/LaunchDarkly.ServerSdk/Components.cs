using System;
using System.Threading.Tasks;
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
        /// <summary>
        /// Returns a factory for the default in-memory implementation of <see cref="IDataStore"/>.
        /// </summary>
        public static IDataStoreFactory InMemoryDataStore => InMemoryDataStoreFactory.Instance;

        /// <summary>
        /// Returns a configurable factory for a persistent data store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method takes an <see cref="IPersistentDataStoreFactory"/> that is provided by
        /// some persistent data store implementation (i.e. a database integration), and converts
        /// it to a <see cref="PersistentDataStoreConfiguration"/> which can be used to add
        /// caching behavior. You can then pass the <see cref="PersistentDataStoreConfiguration"/>
        /// object to <see cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/> to use this
        /// configuration in the SDK. Example usage:
        /// </para>
        /// <code>
        ///     var myStore = Components.PersistentStore(Redis.FeatureStore())
        ///         .CacheTtl(TimeSpan.FromSeconds(45));
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataStore(myStore)
        ///         .Build();
        /// </code>
        /// <para>
        /// The method is overloaded because some persistent data store implementations
        /// use <see cref="IPersistentDataStoreFactory"/> while others use
        /// <see cref="IPersistentDataStoreAsyncFactory"/>.
        /// </para>
        /// </remarks>
        /// <param name="storeFactory">the factory for the underlying data store</param>
        /// <returns>a <see cref="PersistentDataStoreConfiguration"/></returns>
        public static PersistentDataStoreConfiguration PersistentStore(IPersistentDataStoreFactory storeFactory)
        {
            return new PersistentDataStoreConfiguration(storeFactory);
        }

        /// <summary>
        /// Returns a configurable factory for a persistent data store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method takes an <see cref="IPersistentDataStoreFactory"/> that is provided by
        /// some persistent data store implementation (i.e. a database integration), and converts
        /// it to a <see cref="PersistentDataStoreConfiguration"/> which can be used to add
        /// caching behavior. You can then pass the <see cref="PersistentDataStoreConfiguration"/>
        /// object to <see cref="IConfigurationBuilder.DataStore(IDataStoreFactory)"/> to use this
        /// configuration in the SDK. Example usage:
        /// </para>
        /// <code>
        ///     var myStore = Components.PersistentStore(Redis.FeatureStore())
        ///         .CacheTtl(TimeSpan.FromSeconds(45));
        ///     var config = Configuration.Builder(sdkKey)
        ///         .DataStore(myStore)
        ///         .Build();
        /// </code>
        /// <para>
        /// The method is overloaded because some persistent data store implementations
        /// use <see cref="IPersistentDataStoreFactory"/> while others use
        /// <see cref="IPersistentDataStoreAsyncFactory"/>.
        /// </para>
        /// </remarks>
        /// <param name="storeFactory">the factory for the underlying data store</param>
        /// <returns>a <see cref="PersistentDataStoreConfiguration"/></returns>
        public static PersistentDataStoreConfiguration PersistentStore(IPersistentDataStoreAsyncFactory storeFactory)
        {
            return new PersistentDataStoreConfiguration(storeFactory);
        }

        /// <summary>
        /// Returns a factory for the default implementation of <see cref="IEventProcessor"/>, which
        /// forwards all analytics events to LaunchDarkly (unless the client is offline).
        /// </summary>
        public static IEventProcessorFactory DefaultEventProcessor => DefaultEventProcessorFactory.Instance;

        /// <summary>
        /// Returns a factory for a null implementation of <see cref="IEventProcessor"/>, which will
        /// discard all analytics events and not send them to LaunchDarkly, regardless of any
        /// other configuration.
        /// </summary>
        public static IEventProcessorFactory NullEventProcessor => NullEventProcessorFactory.Instance;

        /// <summary>
        /// Returns a factory for the default implementation of <see cref="IDataSource"/>, which
        /// receives feature flag data from LaunchDarkly using either streaming or polling as configured
        /// (or does nothing if the client is offline, or in LDD mode).
        /// </summary>
        public static IDataSourceFactory DefaultDataSource => DefaultDataSourceFactory.Instance;

        /// <summary>
        /// Returns a factory for a null implementation of <see cref="IDataSource"/>, which
        /// does not connect to LaunchDarkly, regardless of any other configuration.
        /// </summary>
        public static IDataSourceFactory NullDataSource => NullDataSourceFactory.Instance;
    }

    internal class DefaultEventProcessorFactory : IEventProcessorFactory
    {
        internal static readonly DefaultEventProcessorFactory Instance = new DefaultEventProcessorFactory();

        private DefaultEventProcessorFactory() { }
        
        public IEventProcessor CreateEventProcessor(LdClientContext context) {
            if (context.Configuration.Offline)
            {
                return new NullEventProcessor();
            }
            else
            {
                return new DefaultEventProcessor(
                    context.Configuration.EventProcessorConfiguration,
                    new DefaultUserDeduplicator(context.Configuration),
                    Util.MakeHttpClient(context.Configuration.HttpRequestConfiguration, ServerSideClientEnvironment.Instance),
                    context.DiagnosticStore,
                    null,
                    null
                );
            }
        }
    }

    internal class NullEventProcessorFactory : IEventProcessorFactory
    {
        internal static readonly NullEventProcessorFactory Instance = new NullEventProcessorFactory();

        private NullEventProcessorFactory() { }

        public IEventProcessor CreateEventProcessor(LdClientContext config) => new NullEventProcessor();
    }

    internal class InMemoryDataStoreFactory : IDataStoreFactory
    {
        internal static readonly InMemoryDataStoreFactory Instance = new InMemoryDataStoreFactory();

        public IDataStore CreateDataStore(LdClientContext context) => new InMemoryDataStore();
    }

    internal class DefaultDataSourceFactory : IDataSourceFactory
    {
        internal static readonly DefaultDataSourceFactory Instance = new DefaultDataSourceFactory();

        private DefaultDataSourceFactory() { }

        // Note, logger uses LDClient class name for backward compatibility
        private static readonly ILog Log = LogManager.GetLogger(typeof(LdClient));

        public IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
        {
            if (context.Configuration.Offline)
            {
                Log.Info("Starting Launchdarkly client in offline mode.");
                return NullDataSource.Instance;
            }
            else if (context.Configuration.UseLdd)
            {
                Log.Info("Starting LaunchDarkly in LDD mode. Skipping direct feature retrieval.");
                return NullDataSource.Instance;
            }
            else
            {
                if (context.Configuration.IsStreamingEnabled)
                {
                    return new StreamProcessor(context.Configuration, dataStoreUpdates, null, context.DiagnosticStore);
                }
                else
                {
                    Log.Warn("You should only disable the streaming API if instructed to do so by LaunchDarkly support");
                    FeatureRequestor requestor = new FeatureRequestor(context.Configuration);
                    return new PollingProcessor(context.Configuration, requestor, dataStoreUpdates);
                }
            }
        }
    }

    /// <summary>
    /// Used when the client is offline or in LDD mode.
    /// </summary>
    internal class NullDataSource : IDataSource
    {
        internal static readonly NullDataSource Instance = new NullDataSource();

        private NullDataSource() { }

        Task<bool> IDataSource.Start() => Task.FromResult(true);

        bool IDataSource.Initialized() => true;

        void IDisposable.Dispose() { }
    }

    internal class NullDataSourceFactory : IDataSourceFactory
    {
        internal static readonly NullDataSourceFactory Instance = new NullDataSourceFactory();

        private NullDataSourceFactory() { }

        public IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates) =>
            NullDataSource.Instance;
    }
}

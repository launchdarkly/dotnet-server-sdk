using System;
using System.Threading.Tasks;
using LaunchDarkly.Logging;
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
        /// Returns a configuration builder for the SDK's logging configuration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Passing this to <see cref="IConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />,
        /// after setting any desired properties on the builder, applies this configuration to the SDK.
        /// </para>
        /// <para>
        /// The default behavior, if you do not change any properties, is to send log output to
        /// <see cref="Console.Error"/>, with a minimum level of <c>Info</c> (that is, <c>Debug</c> logging
        /// is disabled).
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging().Level(LogLevel.Warn)))
        ///         .Build();
        /// </example>
        /// <returns>a configurable factory object</returns>
        /// <seealso cref="IConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />
        /// <seealso cref="Components.Logging(ILogAdapter) "/>
        /// <seealso cref="Components.NoLogging" />
        public static LoggingConfigurationBuilder Logging() =>
            new LoggingConfigurationBuilder();

        /// <summary>
        /// Returns a configuration builder for the SDK's logging configuration, specifying the logging implementation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a shortcut for calling <see cref="Logging()"/> and then
        /// <see cref="LoggingConfigurationBuilder.Adapter(ILogAdapter)"/>, to specify a logging implementation
        /// other than the default one. For instance, in a .NET Core application you can use
        /// <c>LaunchDarkly.Logging.Logs.CoreLogging</c> to use the standard .NET Core logging framework.
        /// </para>
        /// <para>
        /// If you do not also specify a minimum logging level with <see cref="LoggingConfigurationBuilder.Level(LaunchDarkly.Logging.LogLevel)"/>,
        /// or with some other filtering mechanism that is defined by an external logging framework, then the
        /// log output will show all logging levels including <c>Debug</c>.
        /// </para>
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.Logging(Logs.CoreLogging(coreLoggingFactory)))
        ///         .Build();
        /// </example>
        /// <param name="adapter">an <c>ILogAdapter</c> for the desired logging implementation</param>
        /// <returns>a configurable factory object</returns>
        /// <seealso cref="IConfigurationBuilder.Logging(ILoggingConfigurationFactory)" />
        /// <seealso cref="Components.Logging() "/>
        /// <seealso cref="Components.NoLogging" />
        public static LoggingConfigurationBuilder Logging(ILogAdapter adapter) =>
            new LoggingConfigurationBuilder().Adapter(adapter);

        /// <summary>
        /// A configuration object that disables logging.
        /// </summary>
        /// <remarks>
        /// This is the same as <c>Logging(LaunchDarkly.Logging.Logs.None)</c>.
        /// </remarks>
        /// <example>
        ///     var config = Configuration.Builder("my-sdk-key")
        ///         .Logging(Components.NoLogging)
        ///         .Build();
        /// </example>
        public static LoggingConfigurationBuilder NoLogging =>
            new LoggingConfigurationBuilder().Adapter(Logs.None);

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
        
        public LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor CreateEventProcessor(LdClientContext context) {
            if (context.Configuration.Offline)
            {
                return new NullEventProcessor();
            }
            else
            {
                var logger = context.Logger.SubLogger(LogNames.EventsSubLog);
                var eventsConfig = context.Configuration.EventProcessorConfiguration;
                var httpClient = Util.MakeHttpClient(context.Configuration.HttpRequestConfiguration, ServerSideClientEnvironment.Instance);
                var eventSender = new DefaultEventSender(httpClient, eventsConfig, logger);
                return new DelegatingEventProcessor(new DefaultEventProcessor(
                    eventsConfig,
                    eventSender,
                    new DefaultUserDeduplicator(context.Configuration),
                    context.DiagnosticStore,
                    null,
                    logger,
                    null
                ));
            }
        }
    }

    internal class NullEventProcessorFactory : IEventProcessorFactory
    {
        internal static readonly NullEventProcessorFactory Instance = new NullEventProcessorFactory();

        private NullEventProcessorFactory() { }

        public LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor CreateEventProcessor(LdClientContext config) => new NullEventProcessor();
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

        public IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
        {
            if (context.Configuration.Offline)
            {
                context.Logger.Info("Starting Launchdarkly client in offline mode.");
                return NullDataSource.Instance;
            }
            else if (context.Configuration.UseLdd)
            {
                context.Logger.Info("Starting LaunchDarkly in LDD mode. Skipping direct feature retrieval.");
                return NullDataSource.Instance;
            }
            else
            {
                if (context.Configuration.IsStreamingEnabled)
                {
                    return new StreamProcessor(context, dataStoreUpdates, null);
                }
                else
                {
                    context.Logger.Warn("You should only disable the streaming API if instructed to do so by LaunchDarkly support");
                    FeatureRequestor requestor = new FeatureRequestor(context);
                    return new PollingProcessor(context, requestor, dataStoreUpdates);
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

    internal class DelegatingEventProcessor : LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor
    {
        private readonly LaunchDarkly.Sdk.Interfaces.IEventProcessor _impl;

        internal DelegatingEventProcessor(LaunchDarkly.Sdk.Interfaces.IEventProcessor impl)
        {
            _impl = impl;
        }

        public void SendEvent(Event e)
        {
            _impl.SendEvent(e);
        }

        public void Flush()
        {
            _impl.Flush();
        }

        public void Dispose()
        {
            _impl.Dispose();
        }
    }

    internal class NullEventProcessor : LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor
    {
        public void SendEvent(Event e) { }
        public void Flush() { }
        public void Dispose() { }
    }
}

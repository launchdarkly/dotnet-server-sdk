﻿using System.Threading.Tasks;
using LaunchDarkly.Sdk.Internal.Events;
using LaunchDarkly.Sdk.Internal.Helpers;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Events;

namespace LaunchDarkly.Sdk.Server.Internal
{
    /// <summary>
    /// This file contains the internal implementations of all non-configurable component factories whose
    /// public factory methods are in <see cref="Components"/>.
    /// </summary>
    internal static class ComponentsImpl
    {
        internal class DefaultEventProcessorFactory : IEventProcessorFactory
        {
            internal static readonly DefaultEventProcessorFactory Instance = new DefaultEventProcessorFactory();

            private DefaultEventProcessorFactory() { }

            public LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor CreateEventProcessor(LdClientContext context)
            {
                if (context.Configuration.Offline)
                {
                    return new NullEventProcessor();
                }
                else
                {
                    var logger = context.Basic.Logger.SubLogger(LogNames.EventsSubLog);
                    var eventsConfig = context.Configuration.EventProcessorConfiguration;
                    var httpClient = Util.MakeHttpClient(context.Configuration.HttpRequestConfiguration,
                        ServerSideClientEnvironment.Instance);
                    var eventSender = new DefaultEventSender(httpClient, eventsConfig, logger);
                    return new DefaultEventProcessorWrapper(new DefaultEventProcessor(
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

        internal sealed class InMemoryDataStoreFactory : IDataStoreFactory
        {
            internal static readonly InMemoryDataStoreFactory Instance = new InMemoryDataStoreFactory();

            public IDataStore CreateDataStore(LdClientContext context) => new InMemoryDataStore();
        }

        internal sealed class NullDataSource : IDataSource
        {
            internal static readonly IDataSource Instance = new NullDataSource();

            public void Dispose() { }

            public bool Initialized() => true;

            public Task<bool> Start() => Task.FromResult(true);
        }

        internal sealed class NullDataSourceFactory : IDataSourceFactory, IDiagnosticDescription
        {
            internal static readonly IDataSourceFactory Instance = new NullDataSourceFactory();

            public IDataSource CreateDataSource(LdClientContext context, IDataStoreUpdates dataStoreUpdates)
            {
                if (context.Basic.Offline)
                {
                    // If they have explicitly called Offline(true) to disable everything, we'll log this slightly
                    // more specific message.
                    context.Basic.Logger.Info("Starting LaunchDarkly client in offline mode");
                }
                else
                {
                    context.Basic.Logger.Info("LaunchDarkly client will not connect to LaunchDarkly for feature flag data");
                }
                return NullDataSource.Instance;
            }

            public LdValue DescribeConfiguration(BasicConfiguration basic)
            {
                // The difference between "offline" and "using the Relay daemon" is irrelevant from the data source's
                // point of view, but we describe them differently in diagnostic events. This is easy because if we were
                // configured to be completely offline... we wouldn't be sending any diagnostic events. Therefore, if
                // Components.externalUpdatesOnly() was specified as the data source and we are sending a diagnostic
                // event, we can assume usingRelayDaemon should be true.
                return LdValue.BuildObject()
                    .Add("customBaseURI", false)
                    .Add("customStreamURI", false)
                    .Add("streamingDisabled", false)
                    .Add("usingRelayDaemon", true)
                    .Build();
            }
        }

        internal sealed class NullEventProcessor : IEventProcessor
        {
            public void SendEvent(LaunchDarkly.Sdk.Interfaces.Event e) { }
            public void Flush() { }
            public void Dispose() { }
        }

        internal class NullEventProcessorFactory : IEventProcessorFactory
        {
            internal static readonly NullEventProcessorFactory Instance = new NullEventProcessorFactory();

            private NullEventProcessorFactory() { }

            public LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor CreateEventProcessor(LdClientContext config) =>
                new NullEventProcessor();
        }
    }
}
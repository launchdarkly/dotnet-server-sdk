using System;
using System.Threading.Tasks;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Subsystems;

namespace LaunchDarkly.Sdk.Server.Internal
{
    /// <summary>
    /// This file contains the internal implementations of all non-configurable component factories whose
    /// public factory methods are in <see cref="Components"/>.
    /// </summary>
    internal static class ComponentsImpl
    {
        internal sealed class InMemoryDataStoreFactory : IComponentConfigurer<IDataStore>, IDiagnosticDescription
        {
            internal static readonly InMemoryDataStoreFactory Instance = new InMemoryDataStoreFactory();

            public IDataStore Build(LdClientContext context) => new InMemoryDataStore();

            public LdValue DescribeConfiguration(LdClientContext _) => LdValue.Of("memory");
        }

        internal sealed class NullDataSource : IDataSource
        {
            internal static readonly IDataSource Instance = new NullDataSource();

            public void Dispose() { }

            public bool Initialized => true;

            public Task<bool> Start() => Task.FromResult(true);
        }

        internal sealed class NullDataSourceFactory : IComponentConfigurer<IDataSource>, IDiagnosticDescription
        {
            internal static readonly IComponentConfigurer<IDataSource> Instance = new NullDataSourceFactory();

            public IDataSource Build(LdClientContext context)
            {
                if (context.Offline)
                {
                    // If they have explicitly called Offline(true) to disable everything, we'll log this slightly
                    // more specific message.
                    context.Logger.Info("Starting LaunchDarkly client in offline mode");
                }
                else
                {
                    context.Logger.Info("LaunchDarkly client will not connect to LaunchDarkly for feature flag data");
                }
                return NullDataSource.Instance;
            }

            public LdValue DescribeConfiguration(LdClientContext context)
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
            public void RecordEvaluationEvent(EventProcessorTypes.EvaluationEvent e) { }
            public void RecordIdentifyEvent(EventProcessorTypes.IdentifyEvent e) { }
            public void RecordCustomEvent(EventProcessorTypes.CustomEvent e) { }
            public void RecordMigrationEvent(EventProcessorTypes.MigrationOpEvent e) { }

            public void Flush() { }
            public bool FlushAndWait(TimeSpan timeout) => true;
            public void Dispose() { }
        }

        internal sealed class NullEventProcessorFactory : IComponentConfigurer<IEventProcessor>
        {
            internal static readonly NullEventProcessorFactory Instance = new NullEventProcessorFactory();

            private NullEventProcessorFactory() { }

            public IEventProcessor Build(LdClientContext config) =>
                new NullEventProcessor();
        }
    }
}

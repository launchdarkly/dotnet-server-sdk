using System.Threading.Tasks;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.DataStores;

namespace LaunchDarkly.Sdk.Server.Internal
{
    /// <summary>
    /// This file contains the internal implementations of all non-configurable component factories whose
    /// public factory methods are in <see cref="Components"/>.
    /// </summary>
    internal static class ComponentsImpl
    {
        internal sealed class InMemoryDataStoreFactory : IDataStoreFactory, IDiagnosticDescription
        {
            internal static readonly InMemoryDataStoreFactory Instance = new InMemoryDataStoreFactory();

            public IDataStore CreateDataStore(LdClientContext context, IDataStoreUpdates _) => new InMemoryDataStore();

            public LdValue DescribeConfiguration(BasicConfiguration _) => LdValue.Of("memory");
        }

        internal sealed class NullDataSource : IDataSource
        {
            internal static readonly IDataSource Instance = new NullDataSource();

            public void Dispose() { }

            public bool Initialized => true;

            public Task<bool> Start() => Task.FromResult(true);
        }

        internal sealed class NullDataSourceFactory : IDataSourceFactory, IDiagnosticDescription
        {
            internal static readonly IDataSourceFactory Instance = new NullDataSourceFactory();

            public IDataSource CreateDataSource(LdClientContext context, IDataSourceUpdates dataSourceUpdates)
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
            public void RecordEvaluationEvent(EventProcessorTypes.EvaluationEvent e) { }
            public void RecordIdentifyEvent(EventProcessorTypes.IdentifyEvent e) { }
            public void RecordCustomEvent(EventProcessorTypes.CustomEvent e) { }
            public void RecordAliasEvent(EventProcessorTypes.AliasEvent e) { }
            public void Flush() { }
            public void Dispose() { }
        }

        internal sealed class NullEventProcessorFactory : IEventProcessorFactory
        {
            internal static readonly NullEventProcessorFactory Instance = new NullEventProcessorFactory();

            private NullEventProcessorFactory() { }

            public LaunchDarkly.Sdk.Server.Interfaces.IEventProcessor CreateEventProcessor(LdClientContext config) =>
                new NullEventProcessor();
        }
    }
}

using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.Events;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientExternalUpdatesOnlyTest : BaseTest
    {
        public LdClientExternalUpdatesOnlyTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void LddModeClientHasNullDataSource()
        {
            var config = BasicConfig()
                .DataSource(Components.ExternalUpdatesOnly)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<ComponentsImpl.NullDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void LddModeClientHasDefaultEventProcessor()
        {
            var config = BasicConfig()
                .DataSource(Components.ExternalUpdatesOnly)
                .Events(null) // BasicConfig sets this to NoEvents, restore it to the default
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessorWrapper> (client._eventProcessor);
            }
        }

        [Fact]
        public void LddModeClientIsInitialized()
        {
            var config = BasicConfig()
                .DataSource(Components.ExternalUpdatesOnly)
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized);
            }
        }

        [Fact]
        public void LddModeClientGetsFlagFromDataStore()
        {
            var dataStore = new InMemoryDataStore();
            TestUtils.UpsertFlag(dataStore,
                new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(true)).Build());
            var config = BasicConfig()
                .DataSource(Components.ExternalUpdatesOnly)
                .DataStore(dataStore.AsSingletonFactory())
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.BoolVariation("key", User.WithKey("user"), false));
            }
        }
    }
}

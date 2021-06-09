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
        private const string sdkKey = "SDK_KEY";

        public LdClientExternalUpdatesOnlyTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void LddModeClientHasNullDataSource()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<ComponentsImpl.NullDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void LddModeClientHasDefaultEventProcessor()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessorWrapper> (client._eventProcessor);
            }
        }

        [Fact]
        public void LddModeClientIsInitialized()
        {
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .Logging(Components.Logging(testLogging))
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
            var config = Configuration.Builder(sdkKey)
                .DataSource(Components.ExternalUpdatesOnly)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.BoolVariation("key", User.WithKey("user"), false));
            }
        }
    }
}

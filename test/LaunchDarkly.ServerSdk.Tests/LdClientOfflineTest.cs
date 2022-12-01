using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientOfflineTest : BaseTest
    {
        public LdClientOfflineTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void OfflineClientHasNullDataSource()
        {
            var config = BasicConfig().Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<ComponentsImpl.NullDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void OfflineClientHasNullEventProcessor()
        {
            var config = BasicConfig().Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<ComponentsImpl.NullEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void OfflineClientIsInitialized()
        {
            var config = BasicConfig().Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized);
            }
        }

        [Fact]
        public void OfflineReturnsDefaultValue()
        {
            var config = BasicConfig().Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal("x", client.StringVariation("key", Context.New("user"), "x"));
            }
        }

        [Fact]
        public void OfflineClientGetsFlagFromDataStore()
        {
            var dataStore = new InMemoryDataStore();
            TestUtils.UpsertFlag(dataStore,
                new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(true)).Build());
            var config = BasicConfig()
                .Offline(true)
                .DataStore(dataStore.AsSingletonFactory<IDataStore>())
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.BoolVariation("key", Context.New("user"), false));
            }
        }

        [Fact]
        public void OfflineClientStartupMessage()
        {
            var config = BasicConfig().Offline(true).Build();
            using (var client = new LdClient(config))
            {
                AssertLogMessage(true, LogLevel.Info, "Starting LaunchDarkly client in offline mode");
            }
        }

        [Fact]
        public void TestSecureModeHash()
        {
            string expectedHash = "aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597";
            Context context = Context.New("Message");
            User contextAsUser = User.WithKey(context.Key);
            var config = BasicConfig().SdkKey("secret").Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal(expectedHash, client.SecureModeHash(context));
                Assert.Equal(expectedHash, client.SecureModeHash(contextAsUser));
            }
        }
    }
}

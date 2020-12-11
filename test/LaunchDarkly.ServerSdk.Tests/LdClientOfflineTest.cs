using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Internal;
using LaunchDarkly.Sdk.Server.Internal.DataStores;
using LaunchDarkly.Sdk.Server.Internal.Model;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    public class LdClientOfflineTest : BaseTest
    {
        private const string sdkKey = "SDK_KEY";

        public LdClientOfflineTest(ITestOutputHelper testOutput) : base(testOutput) { }

        [Fact]
        public void OfflineClientHasNullDataSource()
        {
            var config = Configuration.Builder(sdkKey).Offline(true)
                .Logging(Components.Logging(testLogging)).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<ComponentsImpl.NullDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void OfflineClientHasNullEventProcessor()
        {
            var config = Configuration.Builder(sdkKey).Offline(true)
                .Logging(Components.Logging(testLogging)).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<ComponentsImpl.NullEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void OfflineClientIsInitialized()
        {
            var config = Configuration.Builder(sdkKey).Offline(true)
                .Logging(Components.Logging(testLogging)).Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }

        [Fact]
        public void OfflineReturnsDefaultValue()
        {
            var config = Configuration.Builder(sdkKey).Offline(true)
                .Logging(Components.Logging(testLogging)).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal("x", client.StringVariation("key", User.WithKey("user"), "x"));
            }
        }

        [Fact]
        public void OfflineClientGetsFlagFromDataStore()
        {
            var dataStore = new InMemoryDataStore();
            TestUtils.UpsertFlag(dataStore,
                new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(true)).Build());
            var config = Configuration.Builder(sdkKey)
                .Offline(true)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .Logging(Components.Logging(testLogging))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal(true, client.BoolVariation("key", User.WithKey("user"), false));
            }
        }

        [Fact]
        public void OfflineClientStartupMessage()
        {
            var config = Configuration.Builder(sdkKey).Offline(true)
                .Logging(Components.Logging(testLogging)).Build();
            using (var client = new LdClient(config))
            {
                Assert.True(logCapture.HasMessageWithText(LogLevel.Info,
                    "Starting LaunchDarkly client in offline mode"), logCapture.ToString());
            }
        }

        [Fact]
        public void TestSecureModeHash()
        {
            var config = Configuration.Builder("secret").Offline(true)
                .Logging(Components.Logging(testLogging)).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597",
                    client.SecureModeHash(User.WithKey("Message")));
            }
        }
    }
}

using LaunchDarkly.Client;
using LaunchDarkly.Client.Interfaces;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientOfflineTest
    {
        [Fact]
        public void OfflineClientHasNullDataSource()
        {
            var config = Configuration.Builder("SDK_KEY").Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullDataSource>(client._dataSource);
            }
        }

        [Fact]
        public void LddModeClientHasNullEventProcessor()
        {
            var config = Configuration.Builder("SDK_KEY").Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void OfflineClientIsInitialized()
        {
            var config = Configuration.Builder("SDK_KEY").Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }

        [Fact]
        public void OfflineReturnsDefaultValue()
        {
            var config = Configuration.Builder("SDK_KEY").Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal("x", client.StringVariation("key", User.WithKey("user"), "x"));
            }
        }

        [Fact]
        public void OfflineClientGetsFlagFromDataStore()
        {
            var dataStore = new InMemoryDataStore();
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(true)).Build());
            var config = Configuration.Builder("SDK_KEY")
                .Offline(true)
                .DataStore(TestUtils.SpecificDataStore(dataStore))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal(true, client.BoolVariation("key", User.WithKey("user"), false));
            }
        }

        [Fact]
        public void TestSecureModeHash()
        {
            var config = Configuration.Builder("secret").Offline(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.Equal("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597",
                    client.SecureModeHash(User.WithKey("Message")));
            }
        }
    }
}

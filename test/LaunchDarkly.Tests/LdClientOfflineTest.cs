using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientOfflineTest
    {
        [Fact]
        public void OfflineClientHasNullUpdateProcessor()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithOffline(true);
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullUpdateProcessor>(client._updateProcessor);
            }
        }

        [Fact]
        public void LddModeClientHasNullEventProcessor()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithOffline(true);
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void OfflineClientIsInitialized()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithOffline(true);
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }

        [Fact]
        public void OfflineReturnsDefaultValue()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithOffline(true);
            using (var client = new LdClient(config))
            {
                Assert.Equal("x", client.StringVariation("key", User.WithKey("user"), "x"));
            }
        }

        [Fact]
        public void OfflineClientGetsFlagFromFeatureStore()
        {
            var featureStore = new InMemoryFeatureStore();
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build());
            var config = Configuration.Default("SDK_KEY")
                .WithOffline(true)
                .WithFeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore));
            using (var client = new LdClient(config))
            {
                Assert.Equal(true, client.BoolVariation("key", User.WithKey("user"), false));
            }
        }

        [Fact]
        public void TestSecureModeHash()
        {
            var config = Configuration.Default("secret")
                .WithOffline(true);
            using (var client = new LdClient(config))
            {
                Assert.Equal("aa747c502a898200f9e4fa21bac68136f886a0e27aec70ba06daf2e2a5cb5597",
                    client.SecureModeHash(User.WithKey("Message")));
            }
        }
    }
}

using LaunchDarkly.Client;
using LaunchDarkly.Common;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientLddModeTest
    {
        [Fact]
        public void LddModeClientHasNullUpdateProcessor()
        {
            var config = Configuration.Builder("SDK_KEY").UseLdd(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullUpdateProcessor>(client._updateProcessor);
            }
        }

        [Fact]
        public void LddModeClientHasDefaultEventProcessor()
        {
            var config = Configuration.Builder("SDK_KEY").UseLdd(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void LddModeClientIsInitialized()
        {
            var config = Configuration.Builder("SDK_KEY").UseLdd(true).Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }

        [Fact]
        public void LddModeClientGetsFlagFromFeatureStore()
        {
            var featureStore = TestUtils.InMemoryFeatureStore();
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build());
            var config = Configuration.Builder("SDK_KEY")
                .UseLdd(true)
                .FeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore))
                .Build();
            using (var client = new LdClient(config))
            {
                Assert.True(client.BoolVariation("key", User.WithKey("user"), false));
            }
        }
    }
}

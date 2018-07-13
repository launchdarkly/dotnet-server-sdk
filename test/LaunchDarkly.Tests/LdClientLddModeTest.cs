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
            Configuration config = Configuration.Default("SDK_KEY")
                .WithUseLdd(true);
            using (var client = new LdClient(config))
            {
                Assert.IsType<NullUpdateProcessor>(client._updateProcessor);
            }
        }

        [Fact]
        public void LddModeClientHasDefaultEventProcessor()
        {
            Configuration config = Configuration.Default("SDK_KEY")
                .WithUseLdd(true);
            using (var client = new LdClient(config))
            {
                Assert.IsType<DefaultEventProcessor>(client._eventProcessor);
            }
        }

        [Fact]
        public void LddModeClientIsInitialized()
        {
            Configuration config = Configuration.Default("SDK_KEY")
                .WithUseLdd(true);
            using (var client = new LdClient(config))
            {
                Assert.True(client.Initialized());
            }
        }

        [Fact]
        public void LddModeClientGetsFlagFromFeatureStore()
        {
            IFeatureStore featureStore = new InMemoryFeatureStore();
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build());
            Configuration config = Configuration.Default("SDK_KEY")
                .WithUseLdd(true)
                .WithFeatureStoreFactory(TestUtils.SpecificFeatureStore(featureStore));
            using (var client = new LdClient(config))
            {
                Assert.True(client.BoolVariation("key", User.WithKey("user"), false));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class LdClientEvaluationTest
    {
        private static readonly User user = User.WithKey("userkey");
        private IFeatureStore featureStore = new InMemoryFeatureStore();
        private ILdClient client;

        public LdClientEvaluationTest()
        {
            var config = Configuration.Default("SDK_KEY")
                .WithFeatureStoreFactory(new SpecificFeatureStoreFactory(featureStore))
                .WithEventProcessorFactory(Components.NullEventProcessor)
                .WithUpdateProcessorFactory(Components.NullUpdateProcessor);
            client = new LdClient(config);
        }

        [Fact]
        public void BoolVariationReturnsFlagValue()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build());

            Assert.True(client.BoolVariation("key", user, false));
        }

        [Fact]
        public void BoolVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.False(client.BoolVariation("key", user, false));
        }

        [Fact]
        public void IntVariationReturnsFlagValue()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build());

            Assert.Equal(2, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void IntVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.Equal(1, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void FloatVariationReturnsFlagValue()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2.5f)).Build());

            Assert.Equal(2.5f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void FloatVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.Equal(1.0f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void StringVariationReturnsFlagValue()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build());

            Assert.Equal("b", client.StringVariation("key", user, "a"));
        }

        [Fact]
        public void StringVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.Equal("a", client.StringVariation("key", user, "a"));
        }

        [Fact]
        public void JsonVariationReturnsFlagValue()
        {
            var data = new JObject();
            data.Add("thing", new JValue("stuff"));
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(data).Build());

            Assert.Equal(data, client.JsonVariation("key", user, new JValue(42)));
        }

        [Fact]
        public void JsonVariationReturnsDefaultValueForUnknownFlag()
        {
            var defaultVal = new JValue(42);
            Assert.Equal(defaultVal, client.JsonVariation("key", user, defaultVal));
        }
        
        [Fact]
        public void CanMatchUserBySegment()
        {
            var segment = new Segment("segment1", 1, new List<string> { user.Key }, null, "", null, false);
            featureStore.Upsert(VersionedDataKind.Segments, segment);

            var clause = new Clause("", "segmentMatch", new List<JValue> { new JValue("segment1") }, false);
            var feature = new FeatureFlagBuilder("feature").BooleanWithClauses(clause).Build();
            featureStore.Upsert(VersionedDataKind.Features, feature);

            Assert.True(client.BoolVariation("feature", user, false));
        }
    }
}

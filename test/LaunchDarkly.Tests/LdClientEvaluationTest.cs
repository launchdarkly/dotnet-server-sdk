using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    // Note, exhaustive coverage of all the code paths for evaluation is in FeatureFlagTest.
    // LdClientEvaluationTest verifies that the LdClient evaluation methods do what they're
    // supposed to do, regardless of exactly what value we get.
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
        public void BoolVariationDetailReturnsValueAndReason()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build());

            var expected = new EvaluationDetail<bool>(true, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.BoolVariationDetail("key", user, false));
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
        public void IntVariationDetailReturnsValueAndReason()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build());

            var expected = new EvaluationDetail<int>(2, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.IntVariationDetail("key", user, 1));
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
        public void FloatVariationDetailReturnsValueAndReason()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2.5f)).Build());

            var expected = new EvaluationDetail<float>(2.5f, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.FloatVariationDetail("key", user, 1.0f));
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
        public void StringVariationDetailReturnsValueAndReason()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build());

            var expected = new EvaluationDetail<string>("b", 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.StringVariationDetail("key", user, "a"));
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
        public void JsonVariationDetailReturnsValueAndReason()
        {
            var data = new JObject();
            data.Add("thing", new JValue("stuff"));
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(data).Build());

            var expected = new EvaluationDetail<JToken>(data, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.JsonVariationDetail("key", user, new JValue(42)));
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

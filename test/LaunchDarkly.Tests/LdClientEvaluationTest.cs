using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json;
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

        [Fact]
        public void AllFlagsReturnsFlagValues()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key1").OffWithValue(new JValue("value1")).Build());
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key2").OffWithValue(new JValue("value2")).Build());

#pragma warning disable 618
            var values = client.AllFlags(user);
#pragma warning restore 618
            var expected = new Dictionary<string, JToken>
            {
                { "key1", "value1" },
                { "key2", "value2"}
            };
            Assert.Equal(expected, values);
        }

        [Fact]
        public void AllFlagsReturnsNullForNulluser()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key1").OffWithValue(new JValue("value1")).Build());
            
#pragma warning disable 618
            var values = client.AllFlags(null);
#pragma warning restore 618
            Assert.Null(values);
        }

        [Fact]
        public void AllFlagsReturnsNullForUserWithNullKey()
        {
            featureStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key1").OffWithValue(new JValue("value1")).Build());

#pragma warning disable 618
            var values = client.AllFlags(User.WithKey(null));
#pragma warning restore 618
            Assert.Null(values);
        }

        [Fact]
        public void AllFlagsStateReturnsState()
        {
            var flag1 = new FeatureFlagBuilder("key1").Version(100)
                .OffVariation(0).Variations(new List<JToken> { new JValue("value1") })
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations(new List<JToken> { new JValue("x"), new JValue("value2") })
                .TrackEvents(true).DebugEventsUntilDate(1000)
                .Build();
            featureStore.Upsert(VersionedDataKind.Features, flag1);
            featureStore.Upsert(VersionedDataKind.Features, flag2);

            var state = client.AllFlagsState(user);
            Assert.True(state.Valid);

            string json = @"{""key1"":""value1"",""key2"":""value2"",
                ""$flagsState"":{
                  ""key1"":{
                    ""variation"":0,""version"":100,""trackEvents"":false
                  },""key2"":{
                    ""variation"":1,""version"":200,""trackEvents"":true,""debugEventsUntilDate"":1000
                  }
                }}";
            var expected = JsonConvert.DeserializeObject<JToken>(json);
            TestUtils.AssertJsonEqual(expected, state.ToJson());
        }

        [Fact]
        public void AllFlagsStateCanFilterForOnlyClientSideFlags()
        {
            var flag1 = new FeatureFlagBuilder("server-side-1").Build();
            var flag2 = new FeatureFlagBuilder("server-side-2").Build();
            var flag3 = new FeatureFlagBuilder("client-side-1").ClientSide(true)
                .OffWithValue("value1").Build();
            var flag4 = new FeatureFlagBuilder("client-side-2").ClientSide(true)
                .OffWithValue("value2").Build();
            featureStore.Upsert(VersionedDataKind.Features, flag1);
            featureStore.Upsert(VersionedDataKind.Features, flag2);
            featureStore.Upsert(VersionedDataKind.Features, flag3);
            featureStore.Upsert(VersionedDataKind.Features, flag4);

            var state = client.AllFlagsState(user, FlagsStateOption.ClientSideOnly);
            Assert.True(state.Valid);

            var expectedValues = new Dictionary<string, JToken>
            {
                { "client-side-1", new JValue("value1") },
                { "client-side-2", new JValue("value2") }
            };
            Assert.Equal(expectedValues, state.ToValuesMap());
        }

        [Fact]
        public void AllFlagsStateReturnsEmptyStateForNullUser()
        {
            var flag = new FeatureFlagBuilder("key1").OffWithValue(new JValue("value1")).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            var state = client.AllFlagsState(null);
            Assert.False(state.Valid);
            Assert.Equal(0, state.ToValuesMap().Count);
        }

        [Fact]
        public void AllFlagsStateReturnsEmptyStateForUserWithNullKey()
        {
            var flag = new FeatureFlagBuilder("key1").OffWithValue(new JValue("value1")).Build();
            featureStore.Upsert(VersionedDataKind.Features, flag);

            var state = client.AllFlagsState(User.WithKey(null));
            Assert.False(state.Valid);
            Assert.Equal(0, state.ToValuesMap().Count);
        }
    }
}

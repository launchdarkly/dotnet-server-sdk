using System.Collections.Generic;
using LaunchDarkly.Client;
using Newtonsoft.Json;
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
        private IDataStore dataStore = new InMemoryDataStore();
        private ILdClient client;

        public LdClientEvaluationTest()
        {
            var config = Configuration.Builder("SDK_KEY")
                .DataStore(new SpecificDataStoreFactory(dataStore))
                .EventProcessorFactory(Components.NullEventProcessor)
                .DataSource(Components.NullDataSource)
                .Build();
            client = new LdClient(config);
        }

        [Fact]
        public void BoolVariationReturnsFlagValue()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build());

            Assert.True(client.BoolVariation("key", user, false));
        }

        [Fact]
        public void BoolVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.False(client.BoolVariation("key", user, false));
        }

        [Fact]
        public void BoolVariationReturnsDefaultValueForWrongType()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("wrong")).Build());

            Assert.Equal(false, client.BoolVariation("key", user, false));
        }

        [Fact]
        public void BoolVariationDetailReturnsValueAndReason()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(true)).Build());

            var expected = new EvaluationDetail<bool>(true, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.BoolVariationDetail("key", user, false));
        }
        
        [Fact]
        public void IntVariationReturnsFlagValue()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build());

            Assert.Equal(2, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void IntVariationReturnsFlagValueEvenIfEncodedAsFloat()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2.25f)).Build());

            Assert.Equal(2, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void IntVariationRoundsToNearestIntFromFloat()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("flag1").OffWithValue(new JValue(2.25f)).Build());
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("flag2").OffWithValue(new JValue(2.75f)).Build());
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("flag3").OffWithValue(new JValue(-2.25f)).Build());
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("flag4").OffWithValue(new JValue(-2.75f)).Build());
            Assert.Equal(2, client.IntVariation("flag1", user, 1));
            Assert.Equal(3, client.IntVariation("flag2", user, 1));
            Assert.Equal(-2, client.IntVariation("flag3", user, 1));
            Assert.Equal(-3, client.IntVariation("flag4", user, 1));
        }

        [Fact]
        public void IntVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.Equal(1, client.IntVariation("key", user, 1));
        }
        
        [Fact]
        public void IntVariationReturnsDefaultValueForWrongType()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("wrong")).Build());

            Assert.Equal(1, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void IntVariationDetailReturnsValueAndReason()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build());

            var expected = new EvaluationDetail<int>(2, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.IntVariationDetail("key", user, 1));
        }

        [Fact]
        public void FloatVariationReturnsFlagValue()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2.5f)).Build());

            Assert.Equal(2.5f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void FloatVariationReturnsFlagValueEvenIfEncodedAsInt()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2)).Build());

            Assert.Equal(2.0f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void FloatVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.Equal(1.0f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void FloatVariationReturnsDefaultValueForWrongType()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("wrong")).Build());

            Assert.Equal(1.0f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void FloatVariationDetailReturnsValueAndReason()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(2.5f)).Build());

            var expected = new EvaluationDetail<float>(2.5f, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.FloatVariationDetail("key", user, 1.0f));
        }

        [Fact]
        public void StringVariationReturnsFlagValue()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build());

            Assert.Equal("b", client.StringVariation("key", user, "a"));
        }

        [Fact]
        public void StringVariationWithNullDefaultReturnsFlagValue()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build());

            Assert.Equal("b", client.StringVariation("key", user, null));
        }

        [Fact]
        public void StringVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.Equal("a", client.StringVariation("key", user, "a"));
        }

        [Fact]
        public void StringVariationWithNullDefaultReturnsDefaultValueForUnknownFlag()
        {
            Assert.Null(client.StringVariation("key", user, null));
        }

        [Fact]
        public void StringVariationReturnsDefaultValueForWrongType()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue(1)).Build());

            Assert.Equal("a", client.StringVariation("key", user, "a"));
        }

        [Fact]
        public void StringVariationDetailReturnsValueAndReason()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build());

            var expected = new EvaluationDetail<string>("b", 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.StringVariationDetail("key", user, "a"));
        }

        [Fact]
        public void JsonVariationReturnsFlagValue()
        {
            var data = LdValue.Convert.String.ObjectFrom(new Dictionary<string, string> { { "thing", "stuff" } });
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(data.InnerValue).Build());

            Assert.Equal(data, client.JsonVariation("key", user, LdValue.Of(42)));
        }

        [Fact]
        public void JsonVariationReturnsDefaultValueForUnknownFlag()
        {
            var defaultVal = LdValue.Of(42);
            Assert.Equal(defaultVal, client.JsonVariation("key", user, defaultVal));
        }

        [Fact]
        public void JsonVariationDetailReturnsValueAndReason()
        {
            var data = LdValue.Convert.String.ObjectFrom(new Dictionary<string, string> { { "thing", "stuff" } });
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(data.InnerValue).Build());

            var expected = new EvaluationDetail<LdValue>(data, 0, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.JsonVariationDetail("key", user, LdValue.Of(42)));
        }
        
        [Fact]
        public void VariationDetailReturnsDefaultForUnknownFlag()
        {
            var expected = new EvaluationDetail<string>("default", null,
                new EvaluationReason.Error(EvaluationErrorKind.FLAG_NOT_FOUND));
            Assert.Equal(expected, client.StringVariationDetail("key", null, "default"));
        }
        
        [Fact]
        public void VariationDetailReturnsDefaultForNullUser()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build());

            var expected = new EvaluationDetail<string>("default", null,
                new EvaluationReason.Error(EvaluationErrorKind.USER_NOT_SPECIFIED));
            Assert.Equal(expected, client.StringVariationDetail("key", null, "default"));
        }

        [Fact]
        public void VariationDetailReturnsDefaultForUserWithNullKey()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("b")).Build());

            var expected = new EvaluationDetail<string>("default", null,
                new EvaluationReason.Error(EvaluationErrorKind.USER_NOT_SPECIFIED));
            Assert.Equal(expected, client.StringVariationDetail("key", User.WithKey(null), "default"));
        }

        [Fact]
        public void VariationDetailReturnsDefaultForFlagThatEvaluatesToNull()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").On(false).OffVariation(null).Build());

            var expected = new EvaluationDetail<string>("default", null, EvaluationReason.Off.Instance);
            Assert.Equal(expected, client.StringVariationDetail("key", user, "default"));
        }

        [Fact]
        public void VariationDetailReturnsDefaultForWrongType()
        {
            dataStore.Upsert(VersionedDataKind.Features,
                new FeatureFlagBuilder("key").OffWithValue(new JValue("wrong")).Build());

            var expected = new EvaluationDetail<int>(1, null,
                new EvaluationReason.Error(EvaluationErrorKind.WRONG_TYPE));
            Assert.Equal(expected, client.IntVariationDetail("key", user, 1));
        }

        [Fact]
        public void CanMatchUserBySegment()
        {
            var segment = new Segment("segment1", 1, new List<string> { user.Key }, null, "", null, false);
            dataStore.Upsert(VersionedDataKind.Segments, segment);

            var clause = new ClauseBuilder().Op("segmentMatch").Values(new JValue("segment1")).Build();
            var feature = new FeatureFlagBuilder("feature").BooleanWithClauses(clause).Build();
            dataStore.Upsert(VersionedDataKind.Features, feature);

            Assert.True(client.BoolVariation("feature", user, false));
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
            dataStore.Upsert(VersionedDataKind.Features, flag1);
            dataStore.Upsert(VersionedDataKind.Features, flag2);

            var state = client.AllFlagsState(user);
            Assert.True(state.Valid);

            var expectedString = @"{""key1"":""value1"",""key2"":""value2"",
                ""$flagsState"":{
                  ""key1"":{
                    ""variation"":0,""version"":100
                  },""key2"":{
                    ""variation"":1,""version"":200,""trackEvents"":true,""debugEventsUntilDate"":1000
                  }
                },
                ""$valid"":true
            }";
            var expectedValue = JsonConvert.DeserializeObject<JToken>(expectedString);
            var actualString = JsonConvert.SerializeObject(state);
            var actualValue = JsonConvert.DeserializeObject<JToken>(actualString);
            TestUtils.AssertJsonEqual(expectedValue, actualValue);
        }

        [Fact]
        public void AllFlagsStateReturnsStateWithReasons()
        {
            var flag1 = new FeatureFlagBuilder("key1").Version(100)
                .OffVariation(0).Variations(new List<JToken> { new JValue("value1") })
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations(new List<JToken> { new JValue("x"), new JValue("value2") })
                .TrackEvents(true).DebugEventsUntilDate(1000)
                .Build();
            dataStore.Upsert(VersionedDataKind.Features, flag1);
            dataStore.Upsert(VersionedDataKind.Features, flag2);

            var state = client.AllFlagsState(user, FlagsStateOption.WithReasons);
            Assert.True(state.Valid);

            var expectedString = @"{""key1"":""value1"",""key2"":""value2"",
                ""$flagsState"":{
                  ""key1"":{
                    ""variation"":0,""version"":100,""reason"":{""kind"":""OFF""}
                  },""key2"":{
                    ""variation"":1,""version"":200,""reason"":{""kind"":""OFF""},""trackEvents"":true,""debugEventsUntilDate"":1000
                  }
                },
                ""$valid"":true
            }";
            var expectedValue = JsonConvert.DeserializeObject<JToken>(expectedString);
            var actualString = JsonConvert.SerializeObject(state);
            var actualValue = JsonConvert.DeserializeObject<JToken>(actualString);
            TestUtils.AssertJsonEqual(expectedValue, actualValue);
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
            dataStore.Upsert(VersionedDataKind.Features, flag1);
            dataStore.Upsert(VersionedDataKind.Features, flag2);
            dataStore.Upsert(VersionedDataKind.Features, flag3);
            dataStore.Upsert(VersionedDataKind.Features, flag4);

            var state = client.AllFlagsState(user, FlagsStateOption.ClientSideOnly);
            Assert.True(state.Valid);

            var expectedValues = new Dictionary<string, LdValue>
            {
                { "client-side-1", LdValue.Of("value1") },
                { "client-side-2", LdValue.Of("value2") }
            };
            Assert.Equal(expectedValues, state.ToValuesJsonMap());
        }

        [Fact]
        public void AllFlagsStateCanOmitDetailsForUntrackedFlags()
        {
            var flag1 = new FeatureFlagBuilder("key1").Version(100)
                .OffVariation(0).Variations(new List<JToken> { new JValue("value1") })
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations(new List<JToken> { new JValue("x"), new JValue("value2") })
                .TrackEvents(true)
                .Build();
            var flag3 = new FeatureFlagBuilder("key3").Version(300)
                .OffVariation(1).Variations(new List<JToken> { new JValue("x"), new JValue("value3") })
                .DebugEventsUntilDate(1000)
                .Build();
            dataStore.Upsert(VersionedDataKind.Features, flag1);
            dataStore.Upsert(VersionedDataKind.Features, flag2);
            dataStore.Upsert(VersionedDataKind.Features, flag3);

            var state = client.AllFlagsState(user, FlagsStateOption.WithReasons);
            Assert.True(state.Valid);

            var expectedString = @"{""key1"":""value1"",""key2"":""value2"",""key3"":""value3"",
                ""$flagsState"":{
                  ""key1"":{
                    ""variation"":0,""version"":100,""reason"":{""kind"":""OFF""}
                  },""key2"":{
                    ""variation"":1,""version"":200,""reason"":{""kind"":""OFF""},""trackEvents"":true
                  },""key3"":{
                    ""variation"":1,""version"":300,""reason"":{""kind"":""OFF""},""debugEventsUntilDate"":1000
                  }
                },
                ""$valid"":true
            }";
            var expectedValue = JsonConvert.DeserializeObject<JToken>(expectedString);
            var actualString = JsonConvert.SerializeObject(state);
            var actualValue = JsonConvert.DeserializeObject<JToken>(actualString);
            TestUtils.AssertJsonEqual(expectedValue, actualValue);
        }

        [Fact]
        public void AllFlagsStateReturnsEmptyStateForNullUser()
        {
            var flag = new FeatureFlagBuilder("key1").OffWithValue(new JValue("value1")).Build();
            dataStore.Upsert(VersionedDataKind.Features, flag);

            var state = client.AllFlagsState(null);
            Assert.False(state.Valid);
            Assert.Equal(0, state.ToValuesJsonMap().Count);
        }

        [Fact]
        public void AllFlagsStateReturnsEmptyStateForUserWithNullKey()
        {
            var flag = new FeatureFlagBuilder("key1").OffWithValue(new JValue("value1")).Build();
            dataStore.Upsert(VersionedDataKind.Features, flag);

            var state = client.AllFlagsState(User.WithKey(null));
            Assert.False(state.Valid);
            Assert.Equal(0, state.ToValuesJsonMap().Count);
        }
    }
}

using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Evaluation;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.TestHelpers;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    // Note, exhaustive coverage of all the code paths for evaluation is in FeatureFlagTest.
    // LdClientEvaluationTest verifies that the LdClient evaluation methods do what they're
    // supposed to do, regardless of exactly what value we get.
    public class LdClientEvaluationTest : BaseTest
    {
        private static readonly User user = User.WithKey("userkey");
        private readonly TestData testData = TestData.DataSource();
        private readonly ILdClient client;

        public LdClientEvaluationTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            var config = BasicConfig()
                .DataSource(testData)
                .Build();
            client = new LdClient(config);
        }

        [Fact]
        public void BoolVariationReturnsFlagValue()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(true)).Build());

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
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("wrong")).Build());

            Assert.False(client.BoolVariation("key", user, false));
        }

        [Fact]
        public void BoolVariationDetailReturnsValueAndReason()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(true)).Build());

            var expected = new EvaluationDetail<bool>(true, 0, EvaluationReason.OffReason);
            Assert.Equal(expected, client.BoolVariationDetail("key", user, false));
        }
        
        [Fact]
        public void IntVariationReturnsFlagValue()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2)).Build());

            Assert.Equal(2, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void IntVariationReturnsFlagValueEvenIfEncodedAsFloat()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2.25f)).Build());

            Assert.Equal(2, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void IntVariationRoundsToNearestIntFromFloat()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag1").OffWithValue(LdValue.Of(2.25f)).Build());
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag2").OffWithValue(LdValue.Of(2.75f)).Build());
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag3").OffWithValue(LdValue.Of(-2.25f)).Build());
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag4").OffWithValue(LdValue.Of(-2.75f)).Build());
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
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("wrong")).Build());

            Assert.Equal(1, client.IntVariation("key", user, 1));
        }

        [Fact]
        public void IntVariationDetailReturnsValueAndReason()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2)).Build());

            var expected = new EvaluationDetail<int>(2, 0, EvaluationReason.OffReason);
            Assert.Equal(expected, client.IntVariationDetail("key", user, 1));
        }

        [Fact]
        public void FloatVariationReturnsFlagValue()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2.5f)).Build());

            Assert.Equal(2.5f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void FloatVariationReturnsFlagValueEvenIfEncodedAsInt()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2)).Build());

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
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("wrong")).Build());

            Assert.Equal(1.0f, client.FloatVariation("key", user, 1.0f));
        }

        [Fact]
        public void FloatVariationDetailReturnsValueAndReason()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2.5f)).Build());

            var expected = new EvaluationDetail<float>(2.5f, 0, EvaluationReason.OffReason);
            Assert.Equal(expected, client.FloatVariationDetail("key", user, 1.0f));
        }

        [Fact]
        public void DoubleVariationReturnsFlagValue()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2.5d)).Build());

            Assert.Equal(2.5d, client.DoubleVariation("key", user, 1.0d));
        }

        [Fact]
        public void DoubleVariationReturnsFlagValueEvenIfEncodedAsInt()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2)).Build());

            Assert.Equal(2.0d, client.DoubleVariation("key", user, 1.0d));
        }

        [Fact]
        public void DoubleVariationReturnsDefaultValueForUnknownFlag()
        {
            Assert.Equal(1.0d, client.DoubleVariation("key", user, 1.0d));
        }

        [Fact]
        public void DoubleVariationReturnsDefaultValueForWrongType()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("wrong")).Build());

            Assert.Equal(1.0d, client.DoubleVariation("key", user, 1.0d));
        }

        [Fact]
        public void DoubleVariationDetailReturnsValueAndReason()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2.5d)).Build());

            var expected = new EvaluationDetail<double>(2.5d, 0, EvaluationReason.OffReason);
            Assert.Equal(expected, client.DoubleVariationDetail("key", user, 1.0d));
        }

        [Fact]
        public void StringVariationReturnsFlagValue()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("b")).Build());

            Assert.Equal("b", client.StringVariation("key", user, "a"));
        }

        [Fact]
        public void StringVariationWithNullDefaultReturnsFlagValue()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("b")).Build());

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
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(1)).Build());

            Assert.Equal("a", client.StringVariation("key", user, "a"));
        }

        [Fact]
        public void StringVariationDetailReturnsValueAndReason()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("b")).Build());

            var expected = new EvaluationDetail<string>("b", 0, EvaluationReason.OffReason);
            Assert.Equal(expected, client.StringVariationDetail("key", user, "a"));
        }

        [Fact]
        public void JsonVariationReturnsFlagValue()
        {
            var data = LdValue.Convert.String.ObjectFrom(new Dictionary<string, string> { { "thing", "stuff" } });
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(data).Build());

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
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(data).Build());

            var expected = new EvaluationDetail<LdValue>(data, 0, EvaluationReason.OffReason);
            Assert.Equal(expected, client.JsonVariationDetail("key", user, LdValue.Of(42)));
        }

        [Fact]
        public void VariationDetailReturnsDefaultForUnknownFlag()
        {
            var expected = new EvaluationDetail<string>("default", null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.FlagNotFound));
            Assert.Equal(expected, client.StringVariationDetail("key", null, "default"));
        }
        
        [Fact]
        public void VariationDetailReturnsDefaultForNullUser()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("b")).Build());

            var expected = new EvaluationDetail<string>("default", null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified));
            Assert.Equal(expected, client.StringVariationDetail("key", null, "default"));
        }

        [Fact]
        public void VariationDetailReturnsDefaultForUserWithNullKey()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("b")).Build());

            var expected = new EvaluationDetail<string>("default", null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified));
            Assert.Equal(expected, client.StringVariationDetail("key", User.WithKey(null), "default"));
        }

        [Fact]
        public void VariationDetailReturnsDefaultForFlagThatEvaluatesToNull()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").On(false).OffVariation(null).Build());

            var expected = new EvaluationDetail<string>("default", null, EvaluationReason.OffReason);
            Assert.Equal(expected, client.StringVariationDetail("key", user, "default"));
        }

        [Fact]
        public void VariationDetailReturnsDefaultForWrongType()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("wrong")).Build());

            var expected = new EvaluationDetail<int>(1, null,
                EvaluationReason.ErrorReason(EvaluationErrorKind.WrongType));
            Assert.Equal(expected, client.IntVariationDetail("key", user, 1));
        }

        [Fact]
        public void CanMatchUserBySegment()
        {
            var segment = new SegmentBuilder("segment`").Version(1).Included(user.Key).Build();
            testData.UsePreconfiguredSegment(segment);

            var clause = new ClauseBuilder().Op("segmentMatch").Values(LdValue.Of(segment.Key)).Build();
            var feature = new FeatureFlagBuilder("feature").BooleanWithClauses(clause).Build();
            testData.UsePreconfiguredFlag(feature);

            Assert.True(client.BoolVariation("feature", user, false));
        }
        
        [Fact]
        public void AllFlagsStateReturnsState()
        {
            var flag1 = new FeatureFlagBuilder("key1").Version(100)
                .OffVariation(0).Variations(LdValue.Of("value1"))
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations(LdValue.Of("x"), LdValue.Of("value2"))
                .TrackEvents(true).DebugEventsUntilDate(UnixMillisecondTime.OfMillis(1000))
                .Build();
            testData.UsePreconfiguredFlag(flag1);
            testData.UsePreconfiguredFlag(flag2);

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
            var actualString = LdJsonSerialization.SerializeObject(state);
            JsonAssertions.AssertJsonEqual(expectedString, actualString);
        }

        [Fact]
        public void AllFlagsStateReturnsStateWithReasons()
        {
            var flag1 = new FeatureFlagBuilder("key1").Version(100)
                .OffVariation(0).Variations(LdValue.Of("value1"))
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations(LdValue.Of("x"), LdValue.Of("value2"))
                .TrackEvents(true).DebugEventsUntilDate(UnixMillisecondTime.OfMillis(1000))
                .Build();
            testData.UsePreconfiguredFlag(flag1);
            testData.UsePreconfiguredFlag(flag2);

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
            var actualString = LdJsonSerialization.SerializeObject(state);
            JsonAssertions.AssertJsonEqual(expectedString, actualString);
        }

        [Fact]
        public void AllFlagsStateCanFilterForOnlyClientSideFlags()
        {
            var flag1 = new FeatureFlagBuilder("server-side-1").Build();
            var flag2 = new FeatureFlagBuilder("server-side-2").Build();
            var flag3 = new FeatureFlagBuilder("client-side-1").ClientSide(true)
                .OffWithValue(LdValue.Of("value1")).Build();
            var flag4 = new FeatureFlagBuilder("client-side-2").ClientSide(true)
                .OffWithValue(LdValue.Of("value2")).Build();
            testData.UsePreconfiguredFlag(flag1);
            testData.UsePreconfiguredFlag(flag2);
            testData.UsePreconfiguredFlag(flag3);
            testData.UsePreconfiguredFlag(flag4);

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
                .OffVariation(0).Variations(LdValue.Of("value1"))
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations(LdValue.Of("x"), LdValue.Of("value2"))
                .TrackEvents(true)
                .Build();
            var flag3 = new FeatureFlagBuilder("key3").Version(300)
                .OffVariation(1).Variations(LdValue.Of("x"), LdValue.Of("value3"))
                .DebugEventsUntilDate(UnixMillisecondTime.OfMillis(1000))
                .Build();
            testData.UsePreconfiguredFlag(flag1);
            testData.UsePreconfiguredFlag(flag2);
            testData.UsePreconfiguredFlag(flag3);

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
            var actualString = LdJsonSerialization.SerializeObject(state);
            JsonAssertions.AssertJsonEqual(expectedString, actualString);
        }

        [Fact]
        public void AllFlagsStateReturnsEmptyStateForNullUser()
        {
            var flag = new FeatureFlagBuilder("key1").OffWithValue(LdValue.Of("value1")).Build();
            testData.UsePreconfiguredFlag(flag);

            var state = client.AllFlagsState(null);
            Assert.False(state.Valid);
            Assert.Equal(0, state.ToValuesJsonMap().Count);
        }

        [Fact]
        public void AllFlagsStateReturnsEmptyStateForUserWithNullKey()
        {
            var flag = new FeatureFlagBuilder("key1").OffWithValue(LdValue.Of("value1")).Build();
            testData.UsePreconfiguredFlag(flag);

            var state = client.AllFlagsState(User.WithKey(null));
            Assert.False(state.Valid);
            Assert.Equal(0, state.ToValuesJsonMap().Count);
        }

        [Fact]
        public void ExceptionWhenGettingOneFlagIsHandledCorrectly()
        {
            // If the data store's Get method throws an error, the expected behavior is that we log
            // a message and return the default value. The exception should not propagate to the caller.
            var ex = new Exception("fake-error");
            var flagKey = "flag-key";
            var mockStore = new Mock<IDataStore>();
            mockStore.Setup(s => s.Get(DataModel.Features, flagKey)).Throws(ex);
            var configWithCustomStore = BasicConfig()
                .DataStore(mockStore.Object.AsSingletonFactory())
                .Build();
            using (var clientWithCustomStore = new LdClient(configWithCustomStore))
            {
                var defaultValue = "default-value";
                var result = clientWithCustomStore.StringVariationDetail("flag-key", user, defaultValue);
                Assert.Equal(defaultValue, result.Value);
                Assert.Null(result.VariationIndex);
                Assert.Equal(EvaluationReason.ErrorReason(EvaluationErrorKind.Exception), result.Reason);
                AssertLogMessageRegex(true, Logging.LogLevel.Error, ex.Message);
            }
        }

        [Fact]
        public void ExceptionWhenEvaluatingOneFlagIsHandledCorrectly()
        {
            // Same as ExceptionWhenGettingOneFlagIsHandledCorrectly, except the exception happens
            // after we've retrieved the flag, when we try to evaluate it. The evaluator logic isn't
            // supposed to throw any exceptions, but you never know, so we've instrumented it with a
            // mechanism for causing a spurious error.
            testData.Update(testData.Flag(Evaluator.FlagKeyToTriggerErrorForTesting));
            var defaultValue = "default-value";
            var result = client.StringVariationDetail(Evaluator.FlagKeyToTriggerErrorForTesting, user, defaultValue);
            Assert.Equal(defaultValue, result.Value);
            Assert.Null(result.VariationIndex);
            Assert.Equal(EvaluationReason.ErrorReason(EvaluationErrorKind.Exception), result.Reason);
            AssertLogMessageRegex(true, Logging.LogLevel.Error, Evaluator.ErrorMessageForTesting);
        }

        [Fact]
        public void ExceptionWhenGettingAllFlagsIsHandledCorrectly()
        {
            // Just like the Variation methods, AllFlagsState should not propagate exceptions from the
            // data store - we don't want to disrupt application code in that way. We'll just set the
            // FeatureFlagsState.Valid property to false to indicate that there was an issue, and log
            // the error.
            var ex = new Exception("fake-error");
            var mockStore = new Mock<IDataStore>();
            mockStore.Setup(s => s.GetAll(DataModel.Features)).Throws(ex);
            var configWithCustomStore = BasicConfig()
                .DataStore(mockStore.Object.AsSingletonFactory())
                .Build();
            using (var clientWithCustomStore = new LdClient(configWithCustomStore))
            {
                var state = clientWithCustomStore.AllFlagsState(user);
                Assert.NotNull(state);
                Assert.False(state.Valid);
                AssertLogMessageRegex(true, Logging.LogLevel.Error, ex.Message);
            }
        }

        [Fact]
        public void ExceptionWhenEvaluatingFlagInAllFlagsIsHandledCorrectly()
        {
            // Same as ExceptionWhenGettingAllFlagsIsHandledCorrectly, except here we get an
            // unexpected exception from the Evaluator for just one flag. The expected behavior is
            // that that flag gets an error result but the rest of the FeatureFlagsState is valid.
            var goodFlagKey = "good-flag";
            testData.Update(testData.Flag(Evaluator.FlagKeyToTriggerErrorForTesting));
            testData.Update(testData.Flag(goodFlagKey).VariationForAllUsers(true));

            var state = client.AllFlagsState(user, FlagsStateOption.WithReasons);
            Assert.NotNull(state);
            Assert.True(state.Valid);
            Assert.Equal(LdValue.Null, state.GetFlagValueJson(Evaluator.FlagKeyToTriggerErrorForTesting));
            Assert.Equal(EvaluationReason.ErrorReason(EvaluationErrorKind.Exception),
                state.GetFlagReason(Evaluator.FlagKeyToTriggerErrorForTesting));
            Assert.Equal(LdValue.Of(true), state.GetFlagValueJson(goodFlagKey));
            AssertLogMessageRegex(true, Logging.LogLevel.Error, Evaluator.ErrorMessageForTesting);
        }
    }
}

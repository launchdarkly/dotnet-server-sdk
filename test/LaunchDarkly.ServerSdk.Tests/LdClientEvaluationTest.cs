using System;
using System.Collections.Generic;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Evaluation;
using LaunchDarkly.Sdk.Server.Internal.Model;
using LaunchDarkly.Sdk.Server.Subsystems;
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
        private static readonly Context context = Context.New("userkey");
        private static readonly User contextAsUser = User.WithKey(context.Key);
        private static readonly Context invalidContext = Context.New("");
        private static readonly User invalidUser = User.WithKey(null);
        private readonly TestData testData = TestData.DataSource();
        private readonly ILdClient client;

        public LdClientEvaluationTest(ITestOutputHelper testOutput) : base(testOutput)
        {
            var config = BasicConfig()
                .DataSource(testData)
                .Build();
            client = new LdClient(config);
        }

        private void DoTypedVariationTests<T>(
            Func<ILdClient, string, Context, T, T> variationMethod,
            Func<ILdClient, string, User, T, T> variationForUserMethod,
            Func<ILdClient, string, Context, T, EvaluationDetail<T>> variationDetailMethod,
            Func<ILdClient, string, User, T, EvaluationDetail<T>> variationDetailForUserMethod,
            T expectedValue,
            LdValue expectedLdValue,
            T defaultValue,
            LdValue wrongTypeLdValue
            )
        {
            string flagKey = "flagkey",
                wrongTypeFlagKey = "wrongtypekey",
                nullValueFlagKey = "nullvaluekey",
                unknownKey = "unknownkey";

            testData.Update(testData.Flag(flagKey).On(true).Variations(LdValue.Null, expectedLdValue)
                .VariationForUser(context.Key, 1));
            testData.Update(testData.Flag(nullValueFlagKey).On(true).Variations(LdValue.Null)
                .VariationForUser(context.Key, 0));
            testData.Update(testData.Flag(wrongTypeFlagKey).On(true).Variations(LdValue.Null, wrongTypeLdValue)
                .VariationForUser(context.Key, 1));

            Assert.Equal(expectedValue, variationMethod(client, flagKey, context, defaultValue));
            Assert.Equal(expectedValue, variationForUserMethod(client, flagKey, contextAsUser, defaultValue));

            Assert.Equal(new EvaluationDetail<T>(expectedValue, 1, EvaluationReason.TargetMatchReason),
                variationDetailMethod(client, flagKey, context, defaultValue));
            Assert.Equal(new EvaluationDetail<T>(expectedValue, 1, EvaluationReason.TargetMatchReason),
                variationDetailForUserMethod(client, flagKey, contextAsUser, defaultValue));

            // unknown flag
            Assert.Equal(defaultValue, variationMethod(client, unknownKey, context, defaultValue));
            Assert.Equal(defaultValue, variationForUserMethod(client, unknownKey, contextAsUser, defaultValue));
            Assert.Equal(new EvaluationDetail<T>(defaultValue, null, EvaluationReason.ErrorReason(EvaluationErrorKind.FlagNotFound)),
                variationDetailMethod(client, unknownKey, context, defaultValue));
            Assert.Equal(new EvaluationDetail<T>(defaultValue, null, EvaluationReason.ErrorReason(EvaluationErrorKind.FlagNotFound)),
                variationDetailForUserMethod(client, unknownKey, contextAsUser, defaultValue));

            // invalid context/user
            Assert.Equal(defaultValue, variationMethod(client, flagKey, invalidContext, defaultValue));
            Assert.Equal(defaultValue, variationForUserMethod(client, flagKey, invalidUser, defaultValue));
            Assert.Equal(new EvaluationDetail<T>(defaultValue, null, EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified)),
                variationDetailMethod(client, flagKey, invalidContext, defaultValue));
            Assert.Equal(new EvaluationDetail<T>(defaultValue, null, EvaluationReason.ErrorReason(EvaluationErrorKind.UserNotSpecified)),
                variationDetailForUserMethod(client, flagKey, invalidUser, defaultValue));

            // wrong type
            if (!wrongTypeLdValue.IsNull)
            {
                Assert.Equal(defaultValue, variationMethod(client, wrongTypeFlagKey, context, defaultValue));
                Assert.Equal(new EvaluationDetail<T>(defaultValue, null, EvaluationReason.ErrorReason(EvaluationErrorKind.WrongType)),
                    variationDetailMethod(client, wrongTypeFlagKey, context, defaultValue));

                // flag value of null is a special case of wrong type, shouldn't happen in real life
                Assert.Equal(defaultValue, variationMethod(client, nullValueFlagKey, context, defaultValue));
                Assert.Equal(new EvaluationDetail<T>(defaultValue, null, EvaluationReason.ErrorReason(EvaluationErrorKind.WrongType)),
                    variationDetailMethod(client, nullValueFlagKey, context, defaultValue));
            }
        }

        [Fact]
        public void BoolEvaluations() =>
            DoTypedVariationTests(
                (c, f, ctx, d) => c.BoolVariation(f, ctx, d),
                (c, f, u, d) => c.BoolVariation(f, u, d),
                (c, f, ctx, d) => c.BoolVariationDetail(f, ctx, d),
                (c, f, u, d) => c.BoolVariationDetail(f, u, d),
                true,
                LdValue.Of(true),
                false,
                LdValue.Of("wrongtype")
                );

        [Fact]
        public void IntEvaluations() =>
            DoTypedVariationTests(
                (c, f, ctx, d) => c.IntVariation(f, ctx, d),
                (c, f, u, d) => c.IntVariation(f, u, d),
                (c, f, ctx, d) => c.IntVariationDetail(f, ctx, d),
                (c, f, u, d) => c.IntVariationDetail(f, u, d),
                2,
                LdValue.Of(2),
                1,
                LdValue.Of("wrongtype")
                );

        [Fact]
        public void FloatEvaluations() =>
            DoTypedVariationTests(
                (c, f, ctx, d) => c.FloatVariation(f, ctx, d),
                (c, f, u, d) => c.FloatVariation(f, u, d),
                (c, f, ctx, d) => c.FloatVariationDetail(f, ctx, d),
                (c, f, u, d) => c.FloatVariationDetail(f, u, d),
                2.5f,
                LdValue.Of(2.5f),
                1.5f,
                LdValue.Of("wrongtype")
                );

        [Fact]
        public void DoubleEvaluations() =>
            DoTypedVariationTests(
                (c, f, ctx, d) => c.DoubleVariation(f, ctx, d),
                (c, f, u, d) => c.DoubleVariation(f, u, d),
                (c, f, ctx, d) => c.DoubleVariationDetail(f, ctx, d),
                (c, f, u, d) => c.DoubleVariationDetail(f, u, d),
                2.5d,
                LdValue.Of(2.5d),
                1.5d,
                LdValue.Of("wrongtype")
                );

        [Fact]
        public void StringEvaluations() =>
            DoTypedVariationTests(
                (c, f, ctx, d) => c.StringVariation(f, ctx, d),
                (c, f, u, d) => c.StringVariation(f, u, d),
                (c, f, ctx, d) => c.StringVariationDetail(f, ctx, d),
                (c, f, u, d) => c.StringVariationDetail(f, u, d),
                "a",
                LdValue.Of("a"),
                "d",
                LdValue.Of(222)
                );

        [Fact]
        public void JsonEvaluations()
        {
            var data = LdValue.Convert.String.ObjectFrom(new Dictionary<string, string> { { "thing", "stuff" } });
            var defaultValue = LdValue.Of(42);

            DoTypedVariationTests(
                (c, f, ctx, d) => c.JsonVariation(f, ctx, d),
                (c, f, u, d) => c.JsonVariation(f, u, d),
                (c, f, ctx, d) => c.JsonVariationDetail(f, ctx, d),
                (c, f, u, d) => c.JsonVariationDetail(f, u, d),
                data,
                data,
                defaultValue,
                LdValue.Null
                );
        }

        [Fact]
        public void IntVariationReturnsFlagValueEvenIfEncodedAsFloat()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2.25f)).Build());

            Assert.Equal(2, client.IntVariation("key", context, 1));
        }

        [Fact]
        public void IntVariationRoundsToNearestIntFromFloat()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag1").OffWithValue(LdValue.Of(2.25f)).Build());
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag2").OffWithValue(LdValue.Of(2.75f)).Build());
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag3").OffWithValue(LdValue.Of(-2.25f)).Build());
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("flag4").OffWithValue(LdValue.Of(-2.75f)).Build());
            Assert.Equal(2, client.IntVariation("flag1", context, 1));
            Assert.Equal(3, client.IntVariation("flag2", context, 1));
            Assert.Equal(-2, client.IntVariation("flag3", context, 1));
            Assert.Equal(-3, client.IntVariation("flag4", context, 1));
        }

        [Fact]
        public void FloatVariationReturnsFlagValueEvenIfEncodedAsInt()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2)).Build());

            Assert.Equal(2.0f, client.FloatVariation("key", context, 1.0f));
        }

        [Fact]
        public void DoubleVariationReturnsFlagValueEvenIfEncodedAsInt()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of(2)).Build());

            Assert.Equal(2.0d, client.DoubleVariation("key", context, 1.0d));
        }

        [Fact]
        public void StringVariationWithNullDefaultReturnsFlagValue()
        {
            testData.UsePreconfiguredFlag(new FeatureFlagBuilder("key").OffWithValue(LdValue.Of("b")).Build());

            Assert.Equal("b", client.StringVariation("key", context, null));
        }

        [Fact]
        public void StringVariationWithNullDefaultReturnsDefaultValueForUnknownFlag()
        {
            Assert.Null(client.StringVariation("key", context, null));
        }

        [Fact]
        public void CanMatchUserBySegment()
        {
            var segment = new SegmentBuilder("segment`").Version(1).Included(context.Key).Build();
            testData.UsePreconfiguredSegment(segment);

            var clause = new ClauseBuilder().Op("segmentMatch").Values(segment.Key).Build();
            var feature = new FeatureFlagBuilder("feature").BooleanWithClauses(clause).Build();
            testData.UsePreconfiguredFlag(feature);

            Assert.True(client.BoolVariation("feature", context, false));
        }
        
        [Fact]
        public void AllFlagsStateReturnsState()
        {
            var flag1 = new FeatureFlagBuilder("key1").Version(100)
                .OffVariation(0).Variations("value1")
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations("x", "value2")
                .TrackEvents(true).DebugEventsUntilDate(UnixMillisecondTime.OfMillis(1000))
                .Build();
            testData.UsePreconfiguredFlag(flag1);
            testData.UsePreconfiguredFlag(flag2);

            var state = client.AllFlagsState(context);
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
                .OffVariation(0).Variations("value1")
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations("x", "value2")
                .TrackEvents(true).DebugEventsUntilDate(UnixMillisecondTime.OfMillis(1000))
                .Build();
            testData.UsePreconfiguredFlag(flag1);
            testData.UsePreconfiguredFlag(flag2);

            var state = client.AllFlagsState(context, FlagsStateOption.WithReasons);
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

            var state = client.AllFlagsState(context, FlagsStateOption.ClientSideOnly);
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
                .OffVariation(0).Variations("value1")
                .Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .OffVariation(1).Variations("x", "value2")
                .TrackEvents(true)
                .Build();
            var flag3 = new FeatureFlagBuilder("key3").Version(300)
                .OffVariation(1).Variations("x", "value3")
                .DebugEventsUntilDate(UnixMillisecondTime.OfMillis(1000))
                .Build();
            testData.UsePreconfiguredFlag(flag1);
            testData.UsePreconfiguredFlag(flag2);
            testData.UsePreconfiguredFlag(flag3);

            var state = client.AllFlagsState(context, FlagsStateOption.WithReasons);
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
        public void AllFlagsStateReturnsEmptyStateForUserWithNullKey()
        {
            var flag = new FeatureFlagBuilder("key1").OffWithValue(LdValue.Of("value1")).Build();
            testData.UsePreconfiguredFlag(flag);

            var state = client.AllFlagsState(Context.New(null));
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
                var result = clientWithCustomStore.StringVariationDetail("flag-key", context, defaultValue);
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
            var result = client.StringVariationDetail(Evaluator.FlagKeyToTriggerErrorForTesting, context, defaultValue);
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
                var state = clientWithCustomStore.AllFlagsState(context);
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
            testData.Update(testData.Flag(goodFlagKey).VariationForAll(true));

            var state = client.AllFlagsState(context, FlagsStateOption.WithReasons);
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

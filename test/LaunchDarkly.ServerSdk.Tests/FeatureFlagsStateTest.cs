using System.Collections.Generic;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.TestHelpers;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public class FeatureFlagsStateTest
    {
        [Fact]
        public void CanGetFlagValue()
        {
            var state = FeatureFlagsState.Builder().AddFlag("key",
                new EvaluationDetail<LdValue>(LdValue.Of("value"), 1, EvaluationReason.OffReason)).Build();

            Assert.Equal(LdValue.Of("value"), state.GetFlagValueJson("key"));
        }

        [Fact]
        public void UnknownFlagReturnsNullValue()
        {
            var state = FeatureFlagsState.Builder().Build();
            Assert.Equal(LdValue.Null, state.GetFlagValueJson("key"));
        }

        [Fact]
        public void CanGetFlagReason()
        {
            var reason = EvaluationReason.FallthroughReason;
            var state = FeatureFlagsState.Builder(FlagsStateOption.WithReasons).AddFlag("key",
                new EvaluationDetail<LdValue>(LdValue.Of("value"), 1, reason)).Build();

            Assert.Equal(EvaluationReason.FallthroughReason, state.GetFlagReason("key"));
        }

        [Fact]
        public void UnknownFlagReturnsNullReason()
        {
            var state = FeatureFlagsState.Builder().Build();

            Assert.Null(state.GetFlagReason("key"));
        }

        [Fact]
        public void ReasonIsNullIfReasonsWereNotRecorded()
        {
            var reason = EvaluationReason.FallthroughReason;
            var state = FeatureFlagsState.Builder().AddFlag("key",
                new EvaluationDetail<LdValue>(LdValue.Of("value"), 1, reason)).Build();

            Assert.Null(state.GetFlagReason("key"));
        }

        [Fact]
        public void CanConvertToValuesMap()
        {
            var state = FeatureFlagsState.Builder()
                .AddFlag("key1", new EvaluationDetail<LdValue>(LdValue.Of("value1"), 1, EvaluationReason.OffReason))
                .AddFlag("key2", new EvaluationDetail<LdValue>(LdValue.Of("value2"), 1, EvaluationReason.OffReason))
                .Build();

            var expected = new Dictionary<string, LdValue>
            {
                { "key1", LdValue.Of("value1") },
                { "key2", LdValue.Of("value2") }
            };
            Assert.Equal(expected, state.ToValuesJsonMap());
        }

        [Fact]
        public void CanSerializeToJson()
        {
            var state = FeatureFlagsState.Builder(FlagsStateOption.WithReasons)
                .AddFlag("key1", LdValue.Of("value1"), 0, EvaluationReason.OffReason, 100, false, null)
                .AddFlag("key2", LdValue.Of("value2"), 1, EvaluationReason.FallthroughReason, 200, true, UnixMillisecondTime.OfMillis(1000))
                .Build();

            var expectedString = @"{""key1"":""value1"",""key2"":""value2"",
                ""$flagsState"":{
                  ""key1"":{
                    ""variation"":0,""version"":100,""reason"":{""kind"":""OFF""}
                  },""key2"":{
                    ""variation"":1,""version"":200,""reason"":{""kind"":""FALLTHROUGH""},""trackEvents"":true,""debugEventsUntilDate"":1000
                  }
                },
                ""$valid"":true
            }";
            var actualString = LdJsonSerialization.SerializeObject(state);
            JsonAssertions.AssertJsonEqual(expectedString, actualString);
        }

        [Fact]
        public void CanDeserializeFromJson()
        {
            var state = FeatureFlagsState.Builder(FlagsStateOption.WithReasons)
                .AddFlag("key1", LdValue.Of("value1"), 0, EvaluationReason.OffReason, 100, false, null)
                .AddFlag("key2", LdValue.Of("value2"), 1, EvaluationReason.FallthroughReason, 200, true, UnixMillisecondTime.OfMillis(1000))
                .Build();

            var jsonString = LdJsonSerialization.SerializeObject(state);
            var state1 = LdJsonSerialization.DeserializeObject<FeatureFlagsState>(jsonString);

            Assert.Equal(state, state1);
        }
    }
}
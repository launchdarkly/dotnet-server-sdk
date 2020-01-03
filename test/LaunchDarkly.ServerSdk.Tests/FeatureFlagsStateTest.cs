using System;
using System.Collections.Generic;
using System.Text;
using LaunchDarkly.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class FeatureFlagsStateTest
    {
        [Fact]
        public void CanGetFlagValue()
        {
            var state = FeatureFlagsState.Builder().AddFlag("key", new EvaluationDetail<LdValue>(LdValue.Of("value"), 1, null)).Build();

            Assert.Equal(LdValue.Of("value"), state.GetFlagValueJson("key"));
        }

        [Fact]
        public void CanGetDeprecatedFlagValue()
        {
            var state = FeatureFlagsState.Builder().AddFlag("key", new EvaluationDetail<LdValue>(LdValue.Of("value"), 1, null)).Build();
#pragma warning disable 0618
            Assert.Equal(new JValue("value"), state.GetFlagValue("key"));
#pragma warning restore 0618
        }

        [Fact]
        public void UnknownFlagReturnsNullValue()
        {
            var state = FeatureFlagsState.Builder().Build();
#pragma warning disable 0618
            Assert.Null(state.GetFlagValue("key"));
#pragma warning restore 0618
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
            var state = FeatureFlagsState.Builder().AddFlag("key", new EvaluationDetail<LdValue>(LdValue.Of("value"), 1, reason)).Build();

            Assert.Null(state.GetFlagReason("key"));
        }

        [Fact]
        public void CanConvertToValuesMap()
        {
            var state = FeatureFlagsState.Builder()
                .AddFlag("key1", new EvaluationDetail<LdValue>(LdValue.Of("value1"), 1, null))
                .AddFlag("key2", new EvaluationDetail<LdValue>(LdValue.Of("value2"), 1, null))
                .Build();

            var expected = new Dictionary<string, LdValue>
            {
                { "key1", LdValue.Of("value1") },
                { "key2", LdValue.Of("value2") }
            };
            Assert.Equal(expected, state.ToValuesJsonMap());
        }

        [Fact]
        public void CanConvertToDeprecatedValuesMap()
        {
            var state = FeatureFlagsState.Builder()
                .AddFlag("key1", new EvaluationDetail<LdValue>(LdValue.Of("value1"), 1, null))
                .AddFlag("key2", new EvaluationDetail<LdValue>(LdValue.Of("value2"), 1, null))
                .Build();

            var expected = new Dictionary<string, JToken>
            {
                { "key1", new JValue("value1") },
                { "key2", new JValue("value2") }
            };
#pragma warning disable 0618
            Assert.Equal(expected, state.ToValuesMap());
#pragma warning restore 0618
        }

        [Fact]
        public void CanSerializeToJson()
        {
            var state = FeatureFlagsState.Builder(FlagsStateOption.WithReasons)
                .AddFlag("key1", new JValue("value1"), 0, null, 100, false, null)
                .AddFlag("key2", new JValue("value2"), 1, EvaluationReason.FallthroughReason, 200, true, 1000)
                .Build();

            var expectedString = @"{""key1"":""value1"",""key2"":""value2"",
                ""$flagsState"":{
                  ""key1"":{
                    ""variation"":0,""version"":100
                  },""key2"":{
                    ""variation"":1,""version"":200,""reason"":{""kind"":""FALLTHROUGH""},""trackEvents"":true,""debugEventsUntilDate"":1000
                  }
                },
                ""$valid"":true
            }";
            var expectedValue = LdValue.Parse(expectedString);
            var actualString = JsonConvert.SerializeObject(state);
            var actualValue = LdValue.Parse(actualString);
            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public void CanDeserializeFromJson()
        {
            var state = FeatureFlagsState.Builder(FlagsStateOption.WithReasons)
                .AddFlag("key1", new JValue("value1"), 0, null, 100, false, null)
                .AddFlag("key2", new JValue("value2"), 1, EvaluationReason.FallthroughReason, 200, true, 1000)
                .Build();

            var jsonString = JsonConvert.SerializeObject(state);
            var state1 = JsonConvert.DeserializeObject<FeatureFlagsState>(jsonString);

            Assert.Equal(state, state1);
        }
    }
}

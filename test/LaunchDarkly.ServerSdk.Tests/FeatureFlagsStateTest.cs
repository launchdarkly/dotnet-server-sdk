using System.Collections.Generic;
using LaunchDarkly.Sdk.Server.Model;
using Newtonsoft.Json;
using Xunit;

namespace LaunchDarkly.Sdk.Server
{
    public class FeatureFlagsStateTest
    {
        [Fact]
        public void CanGetFlagValue()
        {
            var state = new FeatureFlagsState(true);
            var flag = new FeatureFlagBuilder("key").Build();
            state.AddFlag(flag, LdValue.Of("value"), 1, null, false);

            Assert.Equal(LdValue.Of("value"), state.GetFlagValueJson("key"));
        }

        [Fact]
        public void UnknownFlagReturnsNullValue()
        {
            var state = new FeatureFlagsState(true);
            Assert.Equal(LdValue.Null, state.GetFlagValueJson("key"));
        }

        [Fact]
        public void CanGetFlagReason()
        {
            var state = new FeatureFlagsState(true);
            var flag = new FeatureFlagBuilder("key").Build();
            state.AddFlag(flag, LdValue.Of("value"), 1, EvaluationReason.FallthroughReason, false);

            Assert.Equal(EvaluationReason.FallthroughReason, state.GetFlagReason("key"));
        }

        [Fact]
        public void UnknownFlagReturnsNullReason()
        {
            var state = new FeatureFlagsState(true);

            Assert.Null(state.GetFlagReason("key"));
        }

        [Fact]
        public void ReasonIsNullIfReasonsWereNotRecorded()
        {
            var state = new FeatureFlagsState(true);
            var flag = new FeatureFlagBuilder("key").Build();
            state.AddFlag(flag, LdValue.Of("value"), 1, null, false);

            Assert.Null(state.GetFlagReason("key"));
        }

        [Fact]
        public void CanConvertToValuesMap()
        {
            var state = new FeatureFlagsState(true);
            var flag1 = new FeatureFlagBuilder("key1").Build();
            var flag2 = new FeatureFlagBuilder("key2").Build();
            state.AddFlag(flag1, LdValue.Of("value1"), 0, null, false);
            state.AddFlag(flag2, LdValue.Of("value2"), 1, null, false);

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
            var state = new FeatureFlagsState(true);
            var flag1 = new FeatureFlagBuilder("key1").Version(100).Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .TrackEvents(true).DebugEventsUntilDate(1000).Build();
            state.AddFlag(flag1, LdValue.Of("value1"), 0, null, false);
            state.AddFlag(flag2, LdValue.Of("value2"), 1, EvaluationReason.FallthroughReason, false);

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
            var state = new FeatureFlagsState(true);
            var flag1 = new FeatureFlagBuilder("key1").Version(100).Build();
            var flag2 = new FeatureFlagBuilder("key2").Version(200)
                .TrackEvents(true).DebugEventsUntilDate(1000).Build();
            state.AddFlag(flag1, LdValue.Of("value1"), 0, null, false);
            state.AddFlag(flag2, LdValue.Of("value2"), 1, EvaluationReason.FallthroughReason, false);

            var jsonString = JsonConvert.SerializeObject(state);
            var state1 = JsonConvert.DeserializeObject<FeatureFlagsState>(jsonString);

            Assert.Equal(state, state1);
        }
    }
}

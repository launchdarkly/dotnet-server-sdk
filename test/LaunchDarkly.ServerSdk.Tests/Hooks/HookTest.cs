using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;
namespace LaunchDarkly.Sdk.Server.Hooks
{
    public class HookTest
    {
        [Fact]
        public void BaseHookCanBeConstructedWithName()
        {
            var hook = new Hook("test");
            Assert.NotNull(hook);
            Assert.Equal("test", hook.Metadata.Name);
        }

        [Fact]
        public void EvaluationSeriesBeforeStagePassesDataThrough()
        {
            var hook = new Hook("test");
            var input = new SeriesDataBuilder().Set("foo", 10).Build();
            var output = hook.BeforeEvaluation(null, input);
            Assert.Equal(input, output);
        }

        [Fact]
        public void EvaluationSeriesForwardsStagePassesDataThrough()
        {
            var hook = new Hook("test");
            var input = new SeriesDataBuilder().Set("foo", 10).Build();
            var output = hook.AfterEvaluation(null, input, new EvaluationDetail<LdValue>());
            Assert.Equal(input, output);
        }


        [Fact]
        public void SeriesDataCannotBeModified()
        {
            var data1 = new SeriesDataBuilder().Build();
            var data2 = new SeriesDataBuilder(data1).Set("foo", 10).Build();
            Assert.False(data1.ContainsKey("foo"));
            Assert.True(data2.ContainsKey("foo"));
        }

        [Fact]
        public void SeriesDataCanBeRetrieved()
        {
            var data = new SeriesDataBuilder()
                .Set("foo", 10)
                .Set("bar", "value")
                .Set("baz", 3.14)
                .Build();

            Assert.Equal(10, data["foo"]);
            Assert.Equal("value", data["bar"]);
            Assert.Equal(3.14, data["baz"]);
        }

        [Fact]
        public void MissingSeriesDataUsesDefaultValues()
        {
            var data = new SeriesDataBuilder().Build();
            Assert.Equal(10, data.GetValueOrDefault("foo", 10));
            Assert.Equal("value", data.GetValueOrDefault("bar", "value"));
            Assert.Equal(3.14, data.GetValueOrDefault("baz", 3.14));
        }
    }
}

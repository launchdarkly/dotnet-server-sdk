using LaunchDarkly.Sdk.Server.Hooks;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Integrations.OpenTelemetry
{
    public class TestTracingHook
    {
        [Fact]
        public void CanConstructTracingHook()
        {
            var hook = TracingHook.Default();
            Assert.NotNull(hook);
            Assert.Equal("LaunchDarkly Tracing Hook", hook.Metadata.Name);
        }

        [Fact]
        public void CanRetrieveActivitySourceName()
        {
            Assert.NotEmpty(TracingHook.ActivitySourceName);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ConfigurationOptionsDoNotThrowExceptions(bool includeVariant, bool createSpans)
        {
            var hook = TracingHook.Builder()
                .IncludeVariant(includeVariant)
                .CreateActivities(createSpans)
                .Build();
            var context = new EvaluationSeriesContext("foo", Context.New("bar"), LdValue.Null, "testMethod");
            var data = hook.BeforeEvaluation(context, new SeriesDataBuilder().Build());
            hook.AfterEvaluation(context, data, new EvaluationDetail<LdValue>());
        }

        [Fact]
        public void CallingDisposeDoesNotThrowException()
        {
            var hook = TracingHook.Default();
            hook.Dispose();
        }
    }
}

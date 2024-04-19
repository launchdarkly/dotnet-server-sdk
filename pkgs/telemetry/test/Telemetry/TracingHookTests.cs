using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using LaunchDarkly.Sdk.Server.Hooks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Telemetry
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TracingHookCreatesRootSpans(bool createSpans)
        {
            ICollection<Activity> exportedItems = new Collection<Activity>();

            var tracerProvider = OpenTelemetry.Sdk.CreateTracerProviderBuilder()
                .AddSource(TracingHook.ActivitySourceName)
                .AddInMemoryExporter(exportedItems)
                .Build();

            var hookUnderTest = TracingHook.Builder().CreateActivities(createSpans).Build();
            var featureKey = "feature-key";
            var context = Context.New("foo");

            var evalContext1 = new EvaluationSeriesContext(featureKey, context, LdValue.Of(true), "LdClient.BoolVariation");
            var data1 = hookUnderTest.BeforeEvaluation(evalContext1, new SeriesDataBuilder().Build());
            hookUnderTest.AfterEvaluation(evalContext1, data1,
                new EvaluationDetail<LdValue>(LdValue.Of(true), 0, EvaluationReason.FallthroughReason));

            var evalContext2 = new EvaluationSeriesContext(featureKey, context, LdValue.Of("default"), "LdClient.StringVariation");
            var data2 = hookUnderTest.BeforeEvaluation(evalContext2, new SeriesDataBuilder().Build());
            hookUnderTest.AfterEvaluation(evalContext2, data2,
                new EvaluationDetail<LdValue>(LdValue.Of("default"), 0, EvaluationReason.FallthroughReason));

            var items = exportedItems.ToList();

            if (createSpans)
            {
                // If we're creating spans, then we should have two Activities, with the correct operation names.
                // To check that they are root spans, check that the parent is null.
                Assert.Equal(2, items.Count);
                Assert.Equal("LdClient.BoolVariation", items[0].OperationName);
                Assert.Equal("LdClient.StringVariation", items[1].OperationName);
                Assert.True(items.All(i => i.Parent == null));
            }
            else
            {
                // Otherwise, there should be no Activities.
                Assert.Empty(exportedItems);
            }
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TracingHookCreatesChildSpans(bool createSpans)
        {
            ICollection<Activity> exportedItems = new Collection<Activity>();

            var testSource = new ActivitySource("test-source", "1.0.0");

            var tracerProvider = OpenTelemetry.Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .AddSource(TracingHook.ActivitySourceName)
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "test-source", serviceVersion: "1.0.0"))
                .AddInMemoryExporter(exportedItems)
                .Build();

            var hookUnderTest = TracingHook.Builder().CreateActivities(createSpans).Build();
            var featureKey = "feature-key";
            var context = Context.New("foo");

            var rootActivity = testSource.StartActivity("root-activity");

            var evalContext1 = new EvaluationSeriesContext(featureKey, context, LdValue.Of(true), "LdClient.BoolVariation");
            var data1 = hookUnderTest.BeforeEvaluation(evalContext1, new SeriesDataBuilder().Build());
            hookUnderTest.AfterEvaluation(evalContext1, data1,
                new EvaluationDetail<LdValue>(LdValue.Of(true), 0, EvaluationReason.FallthroughReason));

            var evalContext2 = new EvaluationSeriesContext(featureKey, context, LdValue.Of("default"), "LdClient.StringVariation");
            var data2 = hookUnderTest.BeforeEvaluation(evalContext2, new SeriesDataBuilder().Build());
            hookUnderTest.AfterEvaluation(evalContext2, data2,
                new EvaluationDetail<LdValue>(LdValue.Of("default"), 0, EvaluationReason.FallthroughReason));

            rootActivity.Stop();

            var items = exportedItems.ToList();

            if (createSpans)
            {
                // If we're creating spans, since there is an existing root span, we should see the children parented
                // to it.
                Assert.Equal(3, items.Count);
                Assert.Equal("LdClient.BoolVariation", items[0].OperationName);
                Assert.Equal("LdClient.StringVariation", items[1].OperationName);
                Assert.Equal("root-activity", items[2].OperationName);
                Assert.Equal(items[2].SpanId, items[0].ParentSpanId);
            }
            else
            {
                // Otherwise, there should only be the root span that was already created.
                Assert.Single(items);
                Assert.Equal("root-activity", items[0].OperationName);
                Assert.Null(items[0].Parent);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TracingHookIncludesVariant(bool includeVariant)
        {
            ICollection<Activity> exportedItems = new Collection<Activity>();

            var testSource = new ActivitySource("test-source", "1.0.0");

            var tracerProvider = OpenTelemetry.Sdk.CreateTracerProviderBuilder()
                .AddSource("test-source")
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "test-source", serviceVersion: "1.0.0"))
                .AddInMemoryExporter(exportedItems)
                .Build();

            var hookUnderTest = TracingHook.Builder().IncludeVariant(includeVariant).Build();
            var featureKey = "feature-key";
            var context = Context.New("foo");

            var rootActivity = testSource.StartActivity("root-activity");

            var evalContext1 = new EvaluationSeriesContext(featureKey, context, LdValue.Of(true), "LdClient.BoolVariation");
            var data1 = hookUnderTest.BeforeEvaluation(evalContext1, new SeriesDataBuilder().Build());
            hookUnderTest.AfterEvaluation(evalContext1, data1,
                new EvaluationDetail<LdValue>(LdValue.Of(true), 0, EvaluationReason.FallthroughReason));

            var evalContext2 = new EvaluationSeriesContext(featureKey, context, LdValue.Of("default"), "LdClient.StringVariation");
            var data2 = hookUnderTest.BeforeEvaluation(evalContext2, new SeriesDataBuilder().Build());
            hookUnderTest.AfterEvaluation(evalContext2, data2,
                new EvaluationDetail<LdValue>(LdValue.Of("default"), 0, EvaluationReason.FallthroughReason));

            rootActivity.Stop();

            var items = exportedItems.ToList();

            Assert.Single(items);
            Assert.Equal("root-activity", items[0].OperationName);

            if (includeVariant)
            {
                // The idea is to check that the span has two events attached to it, and those events contain the feature
                // flag variants. It's awkward to check because we don't know the exact order of the events or those
                // events' tags.
                var events = items[0].Events;
                Assert.Single(events.Where(e =>
                    e.Tags.Contains(new KeyValuePair<string, object>("feature_flag.variant", "true"))));
                Assert.Single(events.Where(e =>
                    e.Tags.Contains(new KeyValuePair<string, object>("feature_flag.variant", "\"default\""))));
            }
            else
            {
                // If not including the variant, then we shouldn't see any variant tag on any events.
                Assert.All(items, i => i.Events.All(e => e.Tags.All(kvp => kvp.Key != "feature_flag.variant")));
            }
        }
    }
}

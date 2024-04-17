using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using LaunchDarkly.Sdk.Server.Hooks;
using LaunchDarkly.Sdk.Server.Integrations;
using LaunchDarkly.Sdk.Server.Integrations.OpenTelemetry;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.Subsystems;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using LaunchDarkly.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server
{
    using SeriesData = ImmutableDictionary<string, object>;
    using CallbackArgs = Tuple<EvaluationSeriesContext, ImmutableDictionary<string, object>>;
    using Callbacks = Dictionary<string, List<Tuple<EvaluationSeriesContext, ImmutableDictionary<string, object>>>>;

    public class LdClientHooksTest : BaseTest
    {


        private class TestHook : Hook
        {
            private readonly Callbacks _befores;
            private readonly Callbacks _afters;
            private readonly SeriesData _beforeResult;

            public TestHook(string id, Callbacks befores, Callbacks afters, SeriesData beforeResult) : base(id)
            {
                _befores = befores;
                _afters = afters;
                _beforeResult = beforeResult;
            }

            public override SeriesData BeforeEvaluation(EvaluationSeriesContext context, SeriesData data)
            {
                _befores[Metadata.Name].Add(new CallbackArgs(context, data));
                return _beforeResult;
            }

            public override SeriesData AfterEvaluation(EvaluationSeriesContext context, SeriesData data,
                EvaluationDetail<LdValue> detail)
            {
                _afters[Metadata.Name].Add(new CallbackArgs(context, data));
                return base.AfterEvaluation(context, data, detail);
            }
        }

        public LdClientHooksTest(ITestOutputHelper testOutput) : base(testOutput)
        {
        }


        private struct Call
        {
            public Action<LdClient, LdValue> Variation { get; set; }
            public LdValue Value { get; set; }
        }


        private Dictionary<string, Call> GenerateVariationMethods(Context context, string flagKey)
        {
            return new Dictionary<string, Call>()
            {
                {
                    Method.BoolVariation, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.BoolVariation(flagKey, context, value.AsBool);
                        },
                        Value = LdValue.Of(true)
                    }
                },

                {
                    Method.BoolVariationDetail, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.BoolVariationDetail(flagKey, context, value.AsBool);
                        },
                        Value = LdValue.Of(true)
                    }
                },
                {
                    Method.StringVariation, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.StringVariation(flagKey, context, value.AsString);
                        },
                        Value = LdValue.Of("default")
                    }
                },
                {
                    Method.StringVariationDetail, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.StringVariationDetail(flagKey, context, value.AsString);
                        },
                        Value = LdValue.Of("default")
                    }
                },
                {
                    Method.IntVariation, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.IntVariation(flagKey, context, value.AsInt);
                        },
                        Value = LdValue.Of(3)
                    }
                },
                {
                    Method.IntVariationDetail, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.IntVariationDetail(flagKey, context, value.AsInt);
                        },
                        Value = LdValue.Of(3)
                    }
                },
                {
                    Method.DoubleVariation, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.DoubleVariation(flagKey, context, value.AsDouble);
                        },
                        Value = LdValue.Of(3.14)
                    }
                },
                {
                    Method.DoubleVariationDetail, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.DoubleVariationDetail(flagKey, context, value.AsDouble);
                        },
                        Value = LdValue.Of(3.14)
                    }
                },
                {
                    Method.FloatVariation, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.FloatVariation(flagKey, context, value.AsFloat);
                        },
                        Value = LdValue.Of(3.14f)
                    }
                },
                {
                    Method.FloatVariationDetail, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.FloatVariationDetail(flagKey, context, value.AsFloat);
                        },
                        Value = LdValue.Of(3.14f)
                    }
                },
                {
                    Method.JsonVariation, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.JsonVariation(flagKey, context, value);
                        },
                        Value = LdValue.ArrayFrom(new List<LdValue>() { LdValue.Of("foo"), LdValue.Of("bar") })
                    }
                },
                {
                    Method.JsonVariationDetail, new Call()
                    {
                        Variation = (client, value) =>
                        {
                            client.JsonVariationDetail(flagKey, context, value);
                        },
                        Value = LdValue.ArrayFrom(new List<LdValue>() { LdValue.Of("foo"), LdValue.Of("bar") })
                    }
                }
            };
        }


        [Fact]
        public void ClientExecutesConfiguredHooks()
        {
            // This test sets up a few hooks and then executes every client variation method to ensure the hooks
            // are being called. There is some complexity because the variation methods are strongly typed, so the types are
            // erased and the methods stored in a dictionary.

            // These hold the arguments that each "beforeEvaluation" and "afterEvaluation" stage receives.
            var befores = new Callbacks();
            var afters = new Callbacks();

            var hookNames = new List<string> { "hook-1", "hook-2", "hook-3" };
            foreach (var name in hookNames)
            {
                befores[name] = new List<CallbackArgs>();
                afters[name] = new List<CallbackArgs>();
            }

            var beforeResult = new SeriesDataBuilder().Set("foo", "bar").Build();

            // The TestHooks will record their arguments when they are invoked in the 'befores' and 'afters' dictionaries.
            var hooks = hookNames.Select((name) => new TestHook(name, befores, afters, beforeResult));

            var config = BasicConfig().Hooks(Components.Hooks(hooks)).Build();

            var flagKey = "nonexistent-flag";
            var context = Context.New("user-key");

            var methods = GenerateVariationMethods(context, flagKey);

            // We want to ensure our iteration order is fixed for the test, so that we can be sure the methods are
            // called in the order we expect.
            var methodsInOrder = methods.Keys.ToList();

            using (var client = new LdClient(config))
            {
                foreach (var method in methodsInOrder)
                {
                    // Invoke the actual client variation method, e.g. BoolVariation, passing in the associated
                    // default value.
                    var args = methods[method];
                    args.Variation(client, args.Value);
                }
            }

            foreach (var series in new Dictionary<string, Callbacks>{{"beforeEvaluation", befores}, {"afterEvaluation", afters}})
            {
                foreach (var kvp in series.Value)
                {
                    var callbacks = kvp.Value;

                    var evalContexts = callbacks.Select(c => c.Item1);
                    var seriesData = callbacks.Select(c => c.Item2);

                    // The callbacks should have executed for the methods we specified in order.
                    Assert.Equal(methodsInOrder, evalContexts.Select(c => c.Method).ToList());
                    // The contect should be the same for all callbacks.
                    Assert.True(evalContexts.All(c => c.Context.Equals(context)));
                    // The flag key should be the same for all callbacks.
                    Assert.True(evalContexts.All(c => c.FlagKey == flagKey));
                    // The value passed to the callback should match the one we associated with the method.
                    Assert.True(evalContexts.All(c => c.DefaultValue == methods[c.Method].Value));

                    if (series.Key == "beforeEvaluation")
                    {
                        // The hook framework passes empty SeriesData into the beforeEvaluation stage.
                        Assert.True(seriesData.All(s => s.Equals(SeriesData.Empty)));
                    }
                    if (series.Key == "afterEvaluation")
                    {
                        // The test hook's beforeEvaluation stage returns a new SeriesData, check that it is correct.
                        Assert.True(seriesData.All(s => s.Equals(beforeResult)));
                    }
                }
            }
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

            var config = BasicConfig()
                .Hooks(Components.Hooks(new[] { TracingHook.Builder().CreateActivities(createSpans).Build() }))
                .Build();


            using (var client = new LdClient(config))
            {
                client.BoolVariation("feature-key", Context.New("foo"), true);
                client.StringVariation("feature-key", Context.New("foo"), "default");
            }

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

            var config = BasicConfig()
                .Hooks(Components.Hooks(new[] { TracingHook.Builder().CreateActivities(createSpans).Build() }))
                .Build();


            var rootActivity = testSource.StartActivity("root-activity");
            using (var client = new LdClient(config))
            {
                client.BoolVariation("feature-key", Context.New("foo"), true);
                client.StringVariation("feature-key", Context.New("foo"), "default");
            }

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

            var config = BasicConfig()
                .Hooks(Components.Hooks(new[] { TracingHook.Builder().IncludeVariant(includeVariant).Build() }))
                .Build();


            var rootActivity = testSource.StartActivity("root-activity");
            using (var client = new LdClient(config))
            {
                client.BoolVariation("feature-key", Context.New("foo"), true);
                client.StringVariation("feature-key", Context.New("foo"), "default");
            }

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

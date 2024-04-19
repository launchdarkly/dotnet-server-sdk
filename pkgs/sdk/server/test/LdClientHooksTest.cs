using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Sdk.Server.Hooks;
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
    }
}

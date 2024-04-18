using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Sdk.Server.Internal.Hooks.Series;
using LaunchDarkly.Sdk.Server.Internal.Hooks.Executor;
using Xunit;
using LogLevel = LaunchDarkly.Logging.LogLevel;

namespace LaunchDarkly.Sdk.Server.Hooks
{
    using SeriesData = ImmutableDictionary<string, object>;

    public class EvaluationSeriesTest : BaseTest
    {
        private class SpyHook : Hook
        {
            private readonly List<string> _recorder;
            public SpyHook(string name, List<string> recorder) : base(name)
            {
                _recorder = recorder;
            }

            public override SeriesData BeforeEvaluation(EvaluationSeriesContext context, SeriesData data)
            {
                _recorder.Add(Metadata.Name + "_before");
                return data;
            }

            public override SeriesData AfterEvaluation(EvaluationSeriesContext context, SeriesData data, EvaluationDetail<LdValue> detail)
            {
                _recorder.Add(Metadata.Name+ "_after");
                return data;
            }
        }

        private class ThrowingHook : SpyHook {
            private readonly string _beforeError;
            private readonly string _afterError;

            public ThrowingHook(string name, List<string> recorder, string beforeError, string afterError) : base(name, recorder)
            {
                _beforeError = beforeError;
                _afterError = afterError;
            }

            public override SeriesData BeforeEvaluation(EvaluationSeriesContext context, SeriesData data)
            {
                if (_beforeError != null)
                {
                    throw new System.Exception(_beforeError);
                }

                return base.BeforeEvaluation(context, data);
            }

            public override SeriesData AfterEvaluation(EvaluationSeriesContext context, SeriesData data, EvaluationDetail<LdValue> detail)
            {
                if (_afterError != null)
                {
                    throw new System.Exception(_afterError);
                }

                return base.AfterEvaluation(context, data, detail);
            }
        }

        private class DisposingHook : Hook
        {
            private bool _disposedValue;

            public bool Disposed => _disposedValue;
            public DisposingHook(string name) : base(name)
            {
                _disposedValue = false;
            }

            protected override void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    _disposedValue = true;
                }
                base.Dispose(disposing);
            }

        }

        [Theory]
        [InlineData(new string[]{}, new string[]{})]
        [InlineData(new []{"a"}, new[]{"a_before", "a_after"})]
        [InlineData(new[]{"a", "b", "c"}, new[]{"a_before", "b_before", "c_before", "c_after", "b_after", "a_after"})]
        public void HooksAreExecutedInLifoOrder(string[] hookNames, string[] executions)
        {
            var got = new List<string>();

            var context = new EvaluationSeriesContext("flag", new Context(), LdValue.Null, Method.BoolVariation);

            var executor = new Executor(TestLogger, hookNames.Select(name => new SpyHook(name, got)));

            executor.EvaluationSeries(context, LdValue.Convert.Bool, () => (new EvaluationDetail<bool>(), null));

            Assert.Equal(executions, got);
        }

        [Fact]
        public void MultipleExceptionsThrownFromDifferentStagesShouldNotPreventOtherStagesFromRunning()
        {
            var got = new List<string>();

            var context = new EvaluationSeriesContext("flag", new Context(), LdValue.Null, Method.BoolVariation);

            var hooks = new List<Hook>()
            {
                new ThrowingHook("a", got, "error in before!", "error in after!"),
                new SpyHook("b", got),
                new ThrowingHook("c", got, null, "error in after!"),
                new SpyHook("d", got),
                new ThrowingHook("e", got,"error in before!", null),
                new SpyHook("f", got)
            };

            var before = new BeforeEvaluation(TestLogger, hooks, EvaluationStage.Order.Forward);
            var after = new AfterEvaluation(TestLogger, hooks, EvaluationStage.Order.Reverse);

            var beforeData = before.Execute(context, null).ToList();

            // Even though some hooks failed, we should still receive back a complete set of SeriesData items.
            Assert.True(beforeData.Count == hooks.Count);

            // All stages should have returned empty data (successful stages because we passed in empty and they didn't modify it,
            // and failed stages because they failed.)
            Assert.True(beforeData.All(d => d.Equals(SeriesData.Empty)));

            var afterData = after.Execute(context, new EvaluationDetail<LdValue>(), beforeData).ToList();

            // Even though some hooks failed, we should still receive back a complete set of SeriesData items.
            Assert.True(afterData.Count == hooks.Count);

            // All stages should have returned empty data (successful stages because we passed in empty and they didn't modify it,
            // and failed stages because they failed.)
            Assert.True(afterData.All(d => d.Equals(SeriesData.Empty)));

            var expected = new List<string> { "b_before", "c_before", "d_before", "f_before", "f_after", "e_after", "d_after", "b_after"};
            Assert.Equal(expected, got);
        }


        [Theory]
        [InlineData("flag-1", "LaunchDarkly Test hook", "before failed", "after failed")]
        [InlineData("flag-2", "test-hook", "before exception!", "after exception!")]
        public void StageFailureLogsExpectedMessages(string flagName, string hookName, string beforeError, string afterError)
        {
            var beforeFailures = new List<Hook>
                { new ThrowingHook(hookName, new List<string>(), beforeError, afterError) };


            var context = new EvaluationSeriesContext(flagName, new Context(), LdValue.Null, Method.BoolVariation);

            var executor = new Executor(TestLogger, beforeFailures);

            executor.EvaluationSeries(context, LdValue.Convert.Bool, () => (new EvaluationDetail<bool>(), null));

            Assert.True(LogCapture.GetMessages().Count == 2);

            LogCapture.HasMessageWithText(LogLevel.Error,
                $"During evaluation of flag \"{flagName}\", stage \"BeforeEvaluation\" of hook \"{hookName}\" reported error: {beforeError}");
            LogCapture.HasMessageWithText(LogLevel.Error,
                $"During evaluation of flag \"{flagName}\", stage \"AfterEvaluation\" of hook \"{hookName}\" reported error: {afterError}");
        }

        [Fact]
        public void DisposeCallsDisposeOnAllHooks()
        {
            var hooks = new List<DisposingHook>
            {
                new DisposingHook("a"),
                new DisposingHook("b"),
                new DisposingHook("c")
            };

            var executor = new Executor(TestLogger, hooks);
            var context = new EvaluationSeriesContext("", Context.New("foo"), LdValue.Null, "bar");

            executor.EvaluationSeries(context,
                LdValue.Convert.Bool, () => (new EvaluationDetail<bool>(), null));

            Assert.True(hooks.All(h => !h.Disposed));
            executor.Dispose();
            Assert.True(hooks.All(h => h.Disposed));
        }
    }
}

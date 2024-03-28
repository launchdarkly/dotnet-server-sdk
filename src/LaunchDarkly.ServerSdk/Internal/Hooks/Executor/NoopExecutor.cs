using System;
using LaunchDarkly.Sdk.Server.Hooks;
using LaunchDarkly.Sdk.Server.Internal.Hooks.Interfaces;
using LaunchDarkly.Sdk.Server.Internal.Model;

namespace LaunchDarkly.Sdk.Server.Internal.Hooks.Executor
{
    /// <summary>
    /// NoopExecutor does not execute any hook logic. It may be used to avoid any overhead associated with hook execution
    /// if the SDK is configured without hooks.
    /// </summary>
    internal sealed class NoopExecutor: IHookExecutor
    {
        public (EvaluationDetail<T>, FeatureFlag) EvaluationSeries<T>(EvaluationSeriesContext context,
            LdValue.Converter<T> converter, Func<(EvaluationDetail<T>, FeatureFlag)> evaluate) => evaluate();

        public void Dispose()
        {
        }
    }
}

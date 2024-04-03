using System;
using LaunchDarkly.Sdk.Server.Hooks;
using LaunchDarkly.Sdk.Server.Internal.Model;

namespace LaunchDarkly.Sdk.Server.Internal.Hooks.Interfaces
{
    /// <summary>
    /// An IHookExecutor is responsible for executing the logic contained in a series of hook stages.
    /// Currently only the EvaluationSeries is specified; additional series should be added to the interface
    /// as required.
    ///
    /// The purpose of this interface is to allow the SDK to swap out the executor based on having any hooks configured or not.
    /// Specifically, if there are no hooks, the interface methods can be no-ops. This may not make sense as more hook types
    /// are added, so the design should be re-visited at that point.
    /// </summary>
    internal interface IHookExecutor: IDisposable
    {
        /// <summary>
        /// EvaluationSeries should run the evaluation series for each configured hook.
        /// </summary>
        /// <param name="context">context for the evaluation series</param>
        /// <param name="evaluate">function to evaluate the flag value</param>
        /// <param name="converter">used to convert the primitive evaluation value into a wrapped <see cref="LdValue"/> suitable for use in hooks</param>
        /// <typeparam name="T">primitive type of the flag value</typeparam>
        /// <returns>the EvaluationDetail returned from the evaluator</returns>
        (EvaluationDetail<T>, FeatureFlag) EvaluationSeries<T>(EvaluationSeriesContext context, LdValue.Converter<T> converter,
            Func<(EvaluationDetail<T>, FeatureFlag)> evaluate);
    }
}

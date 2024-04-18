using System;
using System.Collections.Immutable;

namespace LaunchDarkly.Sdk.Server.Hooks
{

    using SeriesData = ImmutableDictionary<string, object>;

    /// <summary>
    /// HookMetadata contains information related to a Hook which can be inspected by the SDK, or within
    /// a hook stage.
    /// </summary>
    public sealed class HookMetadata
    {
        /// <summary>
        /// Constructs a new HookMetadata with the given hook name.
        /// </summary>
        /// <param name="name">name of the hook. May be used in logs by the SDK</param>
        public HookMetadata(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Returns the name of the hook.
        /// </summary>
        public string Name { get; }
    }

    /// <summary>
    /// A Hook is a set of user-defined callbacks that are executed by the SDK at various points
    /// of interest. To create your own hook with customized logic, derive from Hook and override its methods.
    ///
    /// Hook currently defines an "evaluation" series, which is composed of two stages:
    /// "beforeEvaluation" and "afterEvaluation".
    ///
    /// These are executed by the SDK before and after the evaluation of a
    /// feature flag.
    ///
    /// Multiple hooks may be configured in the SDK. By default, the SDK will execute each hook's beforeEvaluation
    /// stage in the order they were configured, and afterEvaluation in reverse order.
    ///
    /// This means the last hook defined will tightly wrap the evaluation process, while hooks defined earlier in the
    /// sequence are nested outside of it.
    /// </summary>
    public class Hook : IDisposable
    {
        /// <summary>
        /// Access this hook's <see cref="HookMetadata"/>.
        /// </summary>
        public HookMetadata Metadata { get; private set; }


        /// <summary>
        /// BeforeEvaluation is executed by the SDK before the evaluation of a feature flag. It does not apply to
        /// evaluations performed during a call to AllFlagsState.
        ///
        /// To pass user-configured data to <see cref="AfterEvaluation"/>, return a modification of the given
        /// <see cref="SeriesData"/>. You may use existing ImmutableDictionary methods, for example:
        ///
        /// <code>
        /// var builder = data.ToBuilder();
        /// builder["foo"] = "bar";
        /// return builder.ToImmutable();
        /// </code>
        ///
        /// Or, you may use the <see cref="SeriesDataBuilder"/> for a fluent API:
        /// <code>
        /// return new SeriesDataBuilder(data).Set("foo", "bar").Build();
        /// </code>
        ///
        /// The modified data is not shared with any other hook. It will be passed to subsequent stages in the evaluation
        /// series, including <see cref="AfterEvaluation"/>.
        ///
        /// </summary>
        /// <param name="context">parameters associated with this evaluation</param>
        /// <param name="data">user-configurable data, currently empty</param>
        /// <returns>user-configurable data, which will be forwarded to <see cref="AfterEvaluation"/></returns>
        public virtual SeriesData BeforeEvaluation(EvaluationSeriesContext context, SeriesData data) =>
            data;


        /// <summary>
        /// AfterEvaluation is executed by the SDK after the evaluation of a feature flag. It does not apply to
        /// evaluations performed during a call to AllFlagsState.
        ///
        /// The function should return the given <see cref="SeriesData"/> unmodified, for forward compatibility with subsequent
        /// stages that may be added.
        ///
        /// </summary>
        /// <param name="context">parameters associated with this evaluation</param>
        /// <param name="data">user-configurable data from the <see cref="BeforeEvaluation"/> stage</param>
        /// <param name="detail">flag evaluation result</param>
        /// <returns>user-configurable data, which is currently unused but may be forwarded to subsequent stages in future versions of the SDK</returns>
        public virtual SeriesData AfterEvaluation(EvaluationSeriesContext context, SeriesData data,
            EvaluationDetail<LdValue> detail) => data;

        /// <summary>
        /// Constructs a new Hook with the given name. The name may be used in log messages.
        /// </summary>
        /// <param name="name">the name of the hook</param>
        public Hook(string name)
        {
            Metadata = new HookMetadata(name);
        }

        /// <summary>
        /// Disposes the hook. This method will be called when the SDK is disposed.
        /// </summary>
        /// <param name="disposing">true if the caller is Dispose, false if the caller is a finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Disposes the hook. This method will be called when the SDK is disposed.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

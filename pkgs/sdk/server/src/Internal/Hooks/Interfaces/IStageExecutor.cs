using System.Collections.Generic;
using System.Collections.Immutable;

namespace LaunchDarkly.Sdk.Server.Internal.Hooks.Interfaces
{
    using SeriesData = ImmutableDictionary<string, object>;

    /// <summary>
    /// The main purpose of these interfaces is to allow the Executor to arbitrarily wrap stage execution logic in
    /// other facilities. For example, if we create a benchmarking utility, it can take an arbitrary IStageExecutor
    /// and itself implement IStageExecutor, but wrap the execution logic in a timer.
    ///
    /// Currently the use of this interface isn't necessary and could be removed in favor of a simpler design where
    /// the executor holds the classes (e.g. BeforeEvaluation, AfterEvaluation) directly.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    internal interface IStageExecutor<in TContext>
    {
        /// <summary>
        /// Implementation should execute the same stage for all hooks with the given context and series data.
        /// </summary>
        /// <param name="context">the context</param>
        /// <param name="data">the pre-existing series data; if null, the implementation should create empty data as necessary</param>
        /// <returns>updated series data</returns>
        IEnumerable<SeriesData> Execute(TContext context, IEnumerable<SeriesData> data);
    }

    internal interface IStageExecutor<in TContext, in TExtra>
    {
        /// <summary>
        /// Implementation should execute the same stage for all the hooks with the given context, series data, and extra
        /// data. This can be used if the stage requires additional data that isn't part of the context.
        /// </summary>
        /// <param name="context">the context</param>
        /// <param name="extra">the extra data</param>
        /// <param name="data">the pre-existing series data; if null, the implementation should create empty data as necessary</param>
        /// <returns>updated series data</returns>
        IEnumerable<SeriesData> Execute(TContext context, TExtra extra, IEnumerable<SeriesData> data);
    }
}

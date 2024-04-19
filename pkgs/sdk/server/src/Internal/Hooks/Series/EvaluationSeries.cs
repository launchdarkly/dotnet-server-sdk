using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Hooks;
using LaunchDarkly.Sdk.Server.Internal.Hooks.Interfaces;

namespace LaunchDarkly.Sdk.Server.Internal.Hooks.Series
{
    using SeriesData = ImmutableDictionary<string, object>;

    /// <summary>
    /// Shared utilities that individual evaluation stages may use.
    /// </summary>
    internal class EvaluationStage
    {

        /// <summary>
        /// Defines the order in which evaluation stages are executed.
        /// </summary>
        public enum Order
        {
            /// <summary>
            /// Stages should be executed in their natural order.
            /// </summary>
            Forward,

            /// <summary>
            /// Stages should be executed in their reverse order.
            /// </summary>
            Reverse
        }

        /// <summary>
        /// Used internally to represent the stage that is being executed.
        /// </summary>
        protected enum Stage
        {
            /// <summary>
            /// Executes directly before flag evaluation occurs.
            /// </summary>
            BeforeEvaluation,

            /// <summary>
            /// Executes directly after flag evaluation occurs.
            /// </summary>
            AfterEvaluation
        }

        protected readonly Order _order;
        private readonly Logger _logger;

        protected EvaluationStage(Logger logger, Order order)
        {
            _logger = logger;
            _order = order;
        }

        protected void LogFailure(EvaluationSeriesContext context, Hook h, Stage stage, Exception e)
        {
            _logger.Error("During evaluation of flag \"{0}\", stage \"{1}\" of hook \"{2}\" reported error: {3}",
                context.FlagKey, h.Metadata.Name, stage.ToString(), e.Message);
        }
    }

    /// <summary>
    /// Represents the "before evaluation" stage of the evaluation series. This component gathers
    /// all such stages from every hook, acting as an aggregate interface.
    /// </summary>
    internal sealed class BeforeEvaluation : EvaluationStage, IStageExecutor<EvaluationSeriesContext>
    {
        private readonly IEnumerable<Hook> _hooks;

        public BeforeEvaluation(Logger logger, IEnumerable<Hook> hooks, Order order) : base(logger, order)
        {
            _hooks = (order == Order.Forward) ? hooks : hooks.Reverse();
        }
        public IEnumerable<SeriesData> Execute(EvaluationSeriesContext context, IEnumerable<SeriesData> _)
        {
            return _hooks.Select(hook =>
            {
                try
                {
                    return hook.BeforeEvaluation(context, SeriesData.Empty);
                }
                catch (Exception e)
                {
                    LogFailure(context, hook, Stage.BeforeEvaluation, e);
                    return SeriesData.Empty;
                }
            }).ToList();
        }
    }

    /// <summary>
    /// Represents the "after evaluation" stage of the evaluation series. This component gathers all
    /// such stages from every hook, acting as an aggregate interface.
    /// </summary>
    internal sealed class AfterEvaluation : EvaluationStage, IStageExecutor<EvaluationSeriesContext, EvaluationDetail<LdValue>>
    {
        private readonly IEnumerable<Hook> _hooks;
        public AfterEvaluation(Logger logger, IEnumerable<Hook> hooks, Order order) : base(logger, order)
        {
            _hooks = (order == Order.Forward) ? hooks : hooks.Reverse();
        }

        public IEnumerable<SeriesData> Execute(EvaluationSeriesContext context, EvaluationDetail<LdValue> detail, IEnumerable<SeriesData> seriesData)
        {
            return _hooks.Zip((_order == Order.Reverse ? seriesData.Reverse() : seriesData), (hook, data) =>
                {
                    try
                    {
                        return hook.AfterEvaluation(context, data, detail);
                    }
                    catch (Exception e)
                    {
                        LogFailure(context, hook, Stage.AfterEvaluation, e);
                        return SeriesData.Empty;
                    }
                }).ToList();
        }
    }
}

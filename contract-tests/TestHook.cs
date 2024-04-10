using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server.Hooks;

namespace TestService
{
    using SeriesData = ImmutableDictionary<string, object>;

    public class TestHook: Hook
    {
        private readonly CallbackService _service;
        private readonly Dictionary<string, LdValue> _before;
        private readonly Dictionary<string, LdValue> _after;

        private readonly string _beforeError;
        private readonly string _afterError;

        public TestHook(string name, CallbackService service, Dictionary<string, LdValue> before, Dictionary<string, LdValue> after, string beforeError, string afterError) : base(name)
        {
            _service = service;
            _before = before;
            _after = after;
            _beforeError = beforeError;
            _afterError = afterError;
        }

        public override SeriesData BeforeEvaluation(EvaluationSeriesContext context, SeriesData data)
        {
            if (_beforeError != null)
            {
                throw new Exception(_beforeError);
            }

            _service.Post("", new EvaluationHookParams()
            {
                EvaluationSeriesContext = context,
                EvaluationSeriesData = data,
                Stage = "beforeEvaluation"
            });


            if (_before == null) return base.BeforeEvaluation(context, data);
            var builder = data.ToBuilder();
            foreach (var entry in _before)
            {
                builder[entry.Key] = entry.Value;
            }

            return builder.ToImmutable();
        }

        public override SeriesData AfterEvaluation(EvaluationSeriesContext context, SeriesData data, EvaluationDetail<LdValue> detail)
        {
            if (_afterError != null)
            {
                throw new Exception(_afterError);
            }

            _service.Post("", new EvaluationHookParams()
            {
                EvaluationSeriesContext = context,
                EvaluationSeriesData = data,
                EvaluationDetail = new EvaluateFlagResponse(){Reason = detail.Reason, VariationIndex = detail.VariationIndex, Value = detail.Value},
                Stage = "afterEvaluation"
            });


            if (_after == null) return base.AfterEvaluation(context, data, detail);
            var builder = data.ToBuilder();
            foreach (var entry in _after)
            {
                builder[entry.Key] = entry.Value;
            }

            return builder.ToImmutable();
        }
    }
}

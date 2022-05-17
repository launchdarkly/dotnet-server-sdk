using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LaunchDarkly.Sdk.Server.Internal.Model;

namespace LaunchDarkly.Sdk.Server.Internal.Evaluation
{
    internal static class EvaluatorTypes
    {
        internal struct EvalResult
        {
            internal EvaluationDetail<LdValue> Result;
            internal readonly IList<PrerequisiteEvalRecord> PrerequisiteEvals;

            internal EvalResult(EvaluationDetail<LdValue> result, IList<PrerequisiteEvalRecord> prerequisiteEvals)
            {
                Result = result;
                PrerequisiteEvals = prerequisiteEvals;
            }
        }

        internal struct PrerequisiteEvalRecord
        {
            internal readonly FeatureFlag PrerequisiteFlag;
            internal readonly string PrerequisiteOfFlagKey;
            internal readonly EvaluationDetail<LdValue> Result;

            internal PrerequisiteEvalRecord(FeatureFlag prerequisiteFlag, string prerequisiteOfFlagKey,
                EvaluationDetail<LdValue> result)
            {
                PrerequisiteFlag = prerequisiteFlag;
                PrerequisiteOfFlagKey = prerequisiteOfFlagKey;
                Result = result;
            }
        }
    }
}

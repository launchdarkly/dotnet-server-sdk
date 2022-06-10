using System;
using System.Collections.Generic;
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

        // A simple stack that keeps track of nested flag/segment keys being evaluated. It is optimized
        // to avoid heap allocations in the most common cases where there is only one level of flag
        // prerequisites or segments.
        internal struct LazyStack<T>
        {
            private bool _hasFirstValue;
            private T _firstValue;
            private List<T> _values;

            internal void Push(T value)
            {
                if (_hasFirstValue)
                {
                    if (_values is null)
                    {
                        _values = new List<T>();
                        _values.Add(_firstValue);
                    }
                    _values.Add(value);
                }
                else
                {
                    _firstValue = value;
                    _hasFirstValue = true;
                }
            }

            internal T Pop()
            {
                if (_values is null || _values.Count == 0)
                {
                    if (!_hasFirstValue)
                    {
                        throw new InvalidOperationException();
                    }
                    _hasFirstValue = false;
                    return _firstValue;
                }
                var value = _values[_values.Count - 1];
                _values.RemoveAt(_values.Count - 1);
                return value;
            }

            internal bool Contains(T value)
            {
                if (_hasFirstValue && _firstValue.Equals(value))
                {
                    return true;
                }
                if (!(_values is null))
                {
                    foreach (var v in _values)
                    {
                        if (v.Equals(value))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        internal class StopEvaluationException : Exception
        {
            internal EvaluationErrorKind ErrorKind { get; }

            internal StopEvaluationException(EvaluationErrorKind errorKind) { ErrorKind = errorKind; }
        }
    }
}

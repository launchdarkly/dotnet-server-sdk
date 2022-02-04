using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal readonly struct Clause
    {
        internal UserAttribute Attribute { get; }
        internal Operator Op { get; }
        internal IEnumerable<LdValue> Values { get; }
        internal bool Negate { get; }
        internal PreprocessedData Preprocessed { get; }

        internal Clause(UserAttribute attribute, Operator op, IEnumerable<LdValue> values, bool negate)
        {
            Attribute = attribute;
            Op = op;
            Values = values ?? Enumerable.Empty<LdValue>();
            Negate = negate;
            Preprocessed = Preprocess(Op, Values);
        }

        private static PreprocessedData Preprocess(Operator op, IEnumerable<LdValue> values)
        {
            if (op == Operator.In)
            {
                return new PreprocessedData(valuesAsSet: values.ToImmutableHashSet());
            }
            if (op == Operator.Matches)
            {
                return PreprocessValues(values, value =>
                    value.IsString ? new PreprocessedValue(regex: new Regex(value.AsString)) :
                    new PreprocessedValue());
            }
            if (op == Operator.Before || op == Operator.After)
            {
                return PreprocessValues(values, value =>
                    new PreprocessedValue(dateTime: Operator.ValueToDate(value) ));
            }
            if (op == Operator.SemVerEqual || op == Operator.SemVerGreaterThan || op == Operator.SemVerLessThan)
            {
                return PreprocessValues(values, value =>
                    new PreprocessedValue(semVer: Operator.ValueToSemVer(value) ));
            }
            return new PreprocessedData();
        }

        private static PreprocessedData PreprocessValues(IEnumerable<LdValue> values, Func<LdValue, PreprocessedValue> fn) =>
            new PreprocessedData
            (
                values: values.Select(value =>
                {
                    try
                    {
                        return fn(value);
                    }
                    catch
                    {
                        // any exception means the value isn't valid for this operator
                        return new PreprocessedValue();
                    }
                }).ToImmutableList()
            );

        internal readonly struct PreprocessedData
        {
            internal ImmutableHashSet<LdValue> ValuesAsSet { get; }
            internal ImmutableList<PreprocessedValue> Values { get; }

            public PreprocessedData(ImmutableHashSet<LdValue> valuesAsSet = null, ImmutableList<PreprocessedValue> values = null)
            {
                ValuesAsSet = valuesAsSet;
                Values = values;
            }
        }

        internal readonly struct PreprocessedValue
        {
            internal Regex Regex { get; }
            internal DateTime? DateTime { get; }
            internal SemanticVersion? SemVer { get; }

            public PreprocessedValue(Regex regex = null, DateTime? dateTime = null, SemanticVersion? semVer = null)
            {
                Regex = regex;
                DateTime = dateTime;
                SemVer = semVer;
            }
        }
    }
}
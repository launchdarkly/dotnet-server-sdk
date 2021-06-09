using System;
using System.Collections.Generic;
using System.Linq;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal sealed class Operator
    {
        private readonly string _name;
        private readonly Func<LdValue, LdValue, Clause.PreprocessedValue?, bool> _fn;

        public static readonly Operator In = new Operator("in", ApplyIn);

        public static readonly Operator StartsWith = new Operator("startsWith",
            StringOperator((a, b) => a.StartsWith(b)));

        public static readonly Operator EndsWith = new Operator("endsWith",
            StringOperator((a, b) => a.EndsWith(b)));

        public static readonly Operator Matches = new Operator("matches", ApplyMatches);

        public static readonly Operator Contains = new Operator("contains",
            StringOperator((a, b) => a.Contains(b)));

        public static readonly Operator LessThan = new Operator("lessThan",
            NumericOperator(-1, -1));

        public static readonly Operator LessThanOrEqual = new Operator("lessThanOrEqual",
            NumericOperator(-1, 0));

        public static readonly Operator GreaterThan = new Operator("greaterThan",
            NumericOperator(1, 1));

        public static readonly Operator GreaterThanOrEqual =
            new Operator("greaterThanOrEqual", NumericOperator(1, 0));

        public static readonly Operator Before =
            new Operator("before", DateTimeOperator(-1));

        public static readonly Operator After =
            new Operator("after", DateTimeOperator(1));

        public static readonly Operator SemVerEqual =
            new Operator("semVerEqual", SemVerOperator(0));

        public static readonly Operator SemVerLessThan =
            new Operator("semVerLessThan", SemVerOperator(-1));

        public static readonly Operator SemVerGreaterThan =
            new Operator("semVerGreaterThan", SemVerOperator(1));

        public static readonly Operator SegmentMatch =
            new Operator("segmentMatch", ApplySegmentMatch);

        public static IEnumerable<Operator> All = new Operator[]
        {
            In, StartsWith, EndsWith, Matches, Contains, LessThan, LessThanOrEqual,
            GreaterThan, GreaterThanOrEqual, Before, After, SemVerEqual,
            SemVerLessThan, SemVerGreaterThan, SegmentMatch
        };

        private static readonly IDictionary<string, Operator> _allOperators =
            All.ToDictionary(op => op.Name, op => op);

        private Operator(string name, Func<LdValue, LdValue, Clause.PreprocessedValue?, bool> fn)
        {
            _name = name;
            _fn = fn;
        }

        public static Operator ForName(string name) =>
            _allOperators.TryGetValue(name, out var op) ? op : new Operator(name, ApplyUnknownOp);

        public string Name => _name;

        public bool Apply(LdValue userValue, LdValue clauseValue, Clause.PreprocessedValue? preprocessed) =>
            !userValue.IsNull && !clauseValue.IsNull && _fn(userValue, clauseValue, preprocessed);

        private static bool ApplyIn(LdValue uValue, LdValue cValue, Clause.PreprocessedValue? pre) =>
            uValue.Equals(cValue);

        private static bool ApplyMatches(LdValue userValue, LdValue clauseValue, Clause.PreprocessedValue? preprocessed) =>
            preprocessed.HasValue && preprocessed.Value.Regex != null && userValue.IsString &&
            preprocessed.Value.Regex.IsMatch(userValue.AsString);

        private static bool ApplySegmentMatch(LdValue uValue, LdValue cValue, Clause.PreprocessedValue? pre) =>
            false; // segmentMatch has to be implemented at an earlier point in the evaluation logic

        private static bool ApplyUnknownOp(LdValue uValue, LdValue cValue, Clause.PreprocessedValue? pre) =>
            false; // all unrecognized operators are treated as non-matches, not errors

        private static Func<LdValue, LdValue, Clause.PreprocessedValue?, bool> StringOperator(
            Func<string, string, bool> stringFn
            ) =>
            (userValue, clauseValue, _) =>
                userValue.IsString && clauseValue.IsString && stringFn(userValue.AsString, clauseValue.AsString);
        // Note that AsString cannot return null for either of these, because then IsString would've been false
        // (a JSON null is not a string).

        private static Func<LdValue, LdValue, Clause.PreprocessedValue?, bool> NumericOperator(
            int desiredComparisonResult,
            int otherDesiredComparisonResult
            ) =>
            (userValue, clauseValue, _) =>
            {
                if (!userValue.IsNumber || !clauseValue.IsNumber)
                {
                    return false;
                }
                var result = userValue.AsDouble.CompareTo(clauseValue.AsDouble);
                return result == desiredComparisonResult || result == otherDesiredComparisonResult;
            };

        private static Func<LdValue, LdValue, Clause.PreprocessedValue?, bool> DateTimeOperator(
            int desiredComparisonResult
            ) =>
            (userValue, clauseValue, preprocessed) =>
            {
                if (!preprocessed.HasValue || !preprocessed.Value.DateTime.HasValue)
                {
                    return false; // if the clause value were a valid date, we would have pre-parsed it
                }
                var userValueDateTime = ValueToDate(userValue);
                return userValueDateTime.HasValue &&
                    (userValueDateTime.Value.CompareTo(preprocessed.Value.DateTime.Value) == desiredComparisonResult);
            };

        private static Func<LdValue, LdValue, Clause.PreprocessedValue?, bool> SemVerOperator(
            int desiredComparisonResult
            ) =>
            (userValue, clauseValue, preprocessed) =>
            {
                if (!preprocessed.HasValue || !preprocessed.Value.SemVer.HasValue)
                {
                    return false; // if the clause value were a valid semver, we would have pre-parsed it
                }
                var userValueSemVer = ValueToSemVer(userValue);
                return userValueSemVer.HasValue &&
                    (userValueSemVer.Value.ComparePrecedence(preprocessed.Value.SemVer.Value) == desiredComparisonResult);
            };

        internal static DateTime? ValueToDate(LdValue value)
        {
            if (value.IsString)
            {
                try
                {
                    return DateTime.Parse(value.AsString).ToUniversalTime();
                }
                catch (FormatException)
                {
                    return null;
                }
            }
            if (value.IsNumber)
            {
                return UnixMillisecondTime.OfMillis(value.AsLong).AsDateTime;
            }
            return null;
        }

        internal static SemanticVersion? ValueToSemVer(LdValue value)
        {
            if (value.IsString)
            {
                try
                {
                    return SemanticVersion.Parse(value.AsString, allowMissingMinorAndPatch: true);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
            return null;
        }
    }
}

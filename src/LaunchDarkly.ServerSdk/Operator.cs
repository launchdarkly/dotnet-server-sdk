using System;
using System.Text.RegularExpressions;
using Common.Logging;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    /// <summary>
    /// This struct saves us from having to create a JValue out of every built-in user attribute when we're
    /// evaluating a clause. Since it's a struct and not a class, it doesn't create any extra heap objects.
    /// 
    /// We can eventually do away with this if we enhance ImmutableJsonValue to store primitive types
    /// directly (like we're doing here) instead of in a JValue wrapper.
    /// </summary>
    internal struct ExpressionValue
    {
        private readonly string _stringValue;
        private readonly JToken _jsonValue;

        private ExpressionValue(string stringValue, JToken jsonValue)
        {
            _stringValue = stringValue;
            _jsonValue = jsonValue;
        }

        internal static ExpressionValue FromString(string s)
        {
            return new ExpressionValue(s, null);
        }

        internal static ExpressionValue FromJsonValue(JToken j)
        {
            return new ExpressionValue(null, j);
        }

        internal bool IsNull => _stringValue is null && _jsonValue is null;

        internal bool IsNumber => !(_jsonValue is null) &&
            (_jsonValue.Type == JTokenType.Integer || _jsonValue.Type == JTokenType.Float);

        internal bool IsString => !(_stringValue is null) || (!(_jsonValue is null) && _jsonValue.Type == JTokenType.String);
        
        internal float AsFloat => IsNumber ? _jsonValue.Value<float>() : 0;

        internal string AsString => _stringValue is null ?
            (_jsonValue is null ? null : _jsonValue.Value<string>()) :
            _stringValue;

        internal DateTime? AsDate
        {
            get
            {
                if (!(_jsonValue is null) && _jsonValue.Type == JTokenType.Date)
                {
                    return _jsonValue.Value<DateTime>().ToUniversalTime();
                }
                if (IsString)
                {
                    string s = AsString;
                    return DateTime.Parse(s).ToUniversalTime();
                }
                if (IsNumber)
                {
                    var value = AsFloat;
                    return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(value);
                }
                return null;
            }
        }
    }

    internal static class Operator
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Operator));

        public static bool Apply(string op, ExpressionValue uValue, ExpressionValue cValue)
        {
            try
            {
                if (uValue.IsNull || cValue.IsNull)
                    return false;

                int comparison;

                switch (op)
                {
                    case "in":
                        if (uValue.Equals(cValue))
                        {
                            return true;
                        }

                        if (uValue.IsString || cValue.IsString)
                        {
                            return StringOperator(uValue, cValue, (a, b) => a.Equals(b));
                        }

                        if (TryCompareNumericValues(uValue, cValue, out comparison))
                        {
                            return comparison == 0;
                        }
                        break;

                    case "endsWith":
                        return StringOperator(uValue, cValue, (a, b) => a.EndsWith(b));
                    case "startsWith":
                        return StringOperator(uValue, cValue, (a, b) => a.StartsWith(b));
                    case "matches":
                        return StringOperator(uValue, cValue, (a, b) => new Regex(b).IsMatch(a));
                    case "contains":
                        return StringOperator(uValue, cValue, (a, b) => a.Contains(b));
                    case "lessThan":
                        if (TryCompareNumericValues(uValue, cValue, out comparison))
                        {
                            return comparison < 0;
                        }
                        break;
                    case "lessThanOrEqual":
                        if (TryCompareNumericValues(uValue, cValue, out comparison))
                        {
                            return comparison <= 0;
                        }
                        break;
                    case "greaterThan":
                        if (TryCompareNumericValues(uValue, cValue, out comparison))
                        {
                            return comparison > 0;
                        }
                        break;
                    case "greaterThanOrEqual":
                        if (TryCompareNumericValues(uValue, cValue, out comparison))
                        {
                            return comparison >= 0;
                        }
                        break;
                    case "before":
                        return DateOperator(uValue, cValue, (a, b) => DateTime.Compare(a, b) < 0);
                    case "after":
                        return DateOperator(uValue, cValue, (a, b) => DateTime.Compare(a, b) > 0);
                    case "semVerEqual":
                        return SemVerOperator(uValue, cValue, (a, b) => a.ComparePrecedence(b) == 0);
                    case "semVerLessThan":
                        return SemVerOperator(uValue, cValue, (a, b) => a.ComparePrecedence(b) < 0);
                    case "semVerGreaterThan":
                        return SemVerOperator(uValue, cValue, (a, b) => a.ComparePrecedence(b) > 0);
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                Log.DebugFormat("Got a possibly expected exception when applying operator: {0} to user Value: {1} and feature flag value: {2}. Exception message: {3}",
                    e,
                    op,
                    uValue,
                    cValue,
                    Util.ExceptionMessage(e));
            }
            return false;
        }

        private static bool TryCompareNumericValues(ExpressionValue x, ExpressionValue y, out int result)
        {
            if (!x.IsNumber || !y.IsNumber)
            {
                result = default(int);
                return false;
            }

            result = x.AsFloat.CompareTo(y.AsFloat);
            return true;
        }

        private static bool IsNumericValue(JValue jValue)
        {
            return (jValue.Type.Equals(JTokenType.Float) || jValue.Type.Equals(JTokenType.Integer));
        }
        
        private static bool StringOperator(ExpressionValue uValue, ExpressionValue cValue, Func<string, string, bool> fn)
        {
            if (uValue.IsString && cValue.IsString)
            {
                string us = uValue.AsString;
                string cs = cValue.AsString;
                if (us != null && cs != null)
                {
                    return fn(us, cs);
                }
            }
            return false;
        }

        private static double? ParseDoubleFromJValue(JValue jValue)
        {
            if (IsNumericValue(jValue))
            {
                return (double) jValue;
            }
            return null;
        }
        
        private static bool DateOperator(ExpressionValue uValue, ExpressionValue cValue, Func<DateTime, DateTime, bool> fn)
        {
            var uDateTime = uValue.AsDate;
            var cDateTime = cValue.AsDate;
            return uDateTime.HasValue && cDateTime.HasValue && fn(uDateTime.Value, cDateTime.Value);
        }

        private static bool SemVerOperator(ExpressionValue uValue, ExpressionValue cValue, Func<SemanticVersion, SemanticVersion, bool> fn)
        {
            var uVersion = ValueToSemVer(uValue);
            var cVersion = ValueToSemVer(cValue);
            return uVersion != null && cVersion != null && fn(uVersion, cVersion);
        }
        
        internal static SemanticVersion ValueToSemVer(ExpressionValue value)
        {
            if (value.IsString && value.AsString != null)
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

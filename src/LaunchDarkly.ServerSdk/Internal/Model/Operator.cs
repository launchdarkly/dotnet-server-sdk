using System;
using System.Text.RegularExpressions;

namespace LaunchDarkly.Sdk.Server.Internal.Model
{
    internal static class Operator
    {
        public static bool Apply(string op, LdValue uValue, LdValue cValue)
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
                    return StringOperator(uValue, cValue, (a, b) =>
                    {
                        try
                        {
                            return new Regex(b).IsMatch(a);
                        }
                        catch (ArgumentException)
                        {
                            return false;
                        }
                        
                    });
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
            return false;
        }

        private static bool TryCompareNumericValues(LdValue x, LdValue y, out int result)
        {
            if (!x.IsNumber || !y.IsNumber)
            {
                result = default(int);
                return false;
            }

            result = x.AsDouble.CompareTo(y.AsDouble);
            return true;
        }
        
        private static bool StringOperator(LdValue uValue, LdValue cValue, Func<string, string, bool> fn)
        {
            if (uValue.IsString && cValue.IsString)
            {
                // Note that AsString cannot return null for either of these, because then the Type
                // would have been JsonValueType.Null
                return fn(uValue.AsString, cValue.AsString);
            }
            return false;
        }
        
        private static bool DateOperator(LdValue uValue, LdValue cValue, Func<DateTime, DateTime, bool> fn)
        {
            var uDateTime = ValueToDate(uValue);
            var cDateTime = ValueToDate(cValue);
            return uDateTime.HasValue && cDateTime.HasValue && fn(uDateTime.Value, cDateTime.Value);
        }

        private static bool SemVerOperator(LdValue uValue, LdValue cValue, Func<SemanticVersion, SemanticVersion, bool> fn)
        {
            var uVersion = ValueToSemVer(uValue);
            var cVersion = ValueToSemVer(cValue);
            return uVersion.HasValue && cVersion.HasValue && fn(uVersion.Value, cVersion.Value);
        }

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
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(value.AsFloat);
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

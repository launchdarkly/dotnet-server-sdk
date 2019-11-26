using System;
using System.Text.RegularExpressions;
using Common.Logging;
using LaunchDarkly.Sdk.Internal.Helpers;

namespace LaunchDarkly.Sdk.Server
{
    internal static class Operator
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Operator));
        
        // This method was formerly part of User. It has been moved here because it is only needed
        // for server-side evaluation logic, specifically for comparing values with an Operator.
        // Note that ImmutableJsonValue.Of(string) is an efficient operation that does not allocate
        // a new object, but just wraps the string in a struct (or returns ImmutableJsonValue.Null
        // if the string is null).
        public static LdValue GetUserAttributeForEvaluation(User user, string attribute)
        {
            switch (attribute)
            {
                case "key":
                    return LdValue.Of(user.Key);
                case "secondary":
                    return LdValue.Of(user.Secondary);
                case "ip":
                    return LdValue.Of(user.IPAddress);
                case "email":
                    return LdValue.Of(user.Email);
                case "avatar":
                    return LdValue.Of(user.Avatar);
                case "firstName":
                    return LdValue.Of(user.FirstName);
                case "lastName":
                    return LdValue.Of(user.LastName);
                case "name":
                    return LdValue.Of(user.Name);
                case "country":
                    return LdValue.Of(user.Country);
                case "anonymous":
                    if (user.AnonymousOptional.HasValue)
                    {
                        return LdValue.Of(user.AnonymousOptional.Value);
                    }
                    return LdValue.Null;
                default:
                    return user.Custom.TryGetValue(attribute, out var customValue) ?
                        customValue : LdValue.Null;
            }
        }

        public static bool Apply(string op, LdValue uValue, LdValue cValue)
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
            return uVersion != null && cVersion != null && fn(uVersion, cVersion);
        }

        internal static DateTime? ValueToDate(LdValue value)
        {
            if (value.IsString)
            {
                return DateTime.Parse(value.AsString).ToUniversalTime();
            }
            if (value.IsNumber)
            {
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(value.AsFloat);
            }
            return null;
        }

        internal static SemanticVersion ValueToSemVer(LdValue value)
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

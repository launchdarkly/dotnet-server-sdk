using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Semver;

namespace LaunchDarkly.Client
{
    public static class Operator
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger("Operator");

        public static bool Apply(string op, JValue uValue, JValue cValue)
        {
            try
            {
                if (uValue == null || cValue == null)
                    return false;
                
                switch (op)
                {
                    case "in":
                        if (uValue.Equals(cValue))
                        {
                            return true;
                        }

                        if (uValue.Type.Equals(JTokenType.String) && cValue.Type.Equals(JTokenType.String))
                        {
                            return uValue.Value<string>().Equals(cValue.Value<string>());
                        }

                        return NumericOperator(uValue, cValue, (a, b) => a == b);

                    case "endsWith":
                        return StringOperator(uValue, cValue, (a, b) => a.EndsWith(b));
                    case "startsWith":
                        return StringOperator(uValue, cValue, (a, b) => a.StartsWith(b));
                    case "matches":
                        return StringOperator(uValue, cValue, (a, b) => new Regex(b).IsMatch(a));
                    case "contains":
                        return StringOperator(uValue, cValue, (a, b) => a.Contains(b));
                    case "lessThan":
                        return NumericOperator(uValue, cValue, (a, b) => a < b);
                    case "lessThanOrEqual":
                        return NumericOperator(uValue, cValue, (a, b) => a <= b);
                    case "greaterThan":
                        return NumericOperator(uValue, cValue, (a, b) => a > b);
                    case "greaterThanOrEqual":
                        return NumericOperator(uValue, cValue, (a, b) => a >= b);
                    case "before":
                        return DateOperator(uValue, cValue, (a, b) => DateTime.Compare(a, b) < 0);
                    case "after":
                        return DateOperator(uValue, cValue, (a, b) => DateTime.Compare(a, b) > 0);
                    case "semVerEqual":
                        return SemVerOperator(uValue, cValue, (a, b) => a.Equals(b));
                    case "semVerLessThan":
                        return SemVerOperator(uValue, cValue, (a, b) => a < b);
                    case "semVerGreaterThan":
                        return SemVerOperator(uValue, cValue, (a, b) => a > b);
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                Logger.LogDebug(e,
                    "Got a possibly expected exception when applying operator: {0} to user Value: {1} and feature flag value: {2}. Exception message: {3}",
                    op,
                    uValue,
                    cValue, 
                    Util.ExceptionMessage(e));
            }
            return false;
        }

        private static bool StringOperator(JValue uValue, JValue cValue, Func<string, string, bool> fn)
        {
            if (uValue.Type.Equals(JTokenType.String) && cValue.Type.Equals(JTokenType.String))
            {
                return fn(uValue.Value<string>(), cValue.Value<string>());
            }
            return false;
        }

        private static double? ParseDoubleFromJValue(JValue jValue)
        {
            if (jValue.Type.Equals(JTokenType.Float) || jValue.Type.Equals(JTokenType.Integer))
            {
                return (double) jValue;
            }
            return null;
        }

        private static bool NumericOperator(JValue uValue, JValue cValue, Func<double, double, bool> fn)
        {
            var uDouble = ParseDoubleFromJValue(uValue);
            var cDouble = ParseDoubleFromJValue(cValue);
            return uDouble.HasValue && cDouble.HasValue && fn(uDouble.Value, cDouble.Value);
        }

        private static bool DateOperator(JValue uValue, JValue cValue, Func<DateTime, DateTime, bool> fn)
        {
            var uDateTime = JValueToDateTime(uValue);
            var cDateTime = JValueToDateTime(cValue);
            return uDateTime.HasValue && cDateTime.HasValue && fn(uDateTime.Value, cDateTime.Value);
        }

        private static bool SemVerOperator(JValue uValue, JValue cValue, Func<SemVersion, SemVersion, bool> fn)
        {
            var uVersion = JValueToSemVer(uValue);
            var cVersion = JValueToSemVer(cValue);
            return uVersion != null && cVersion != null && fn(uVersion, cVersion);
        }

        //Visible for testing
        public static DateTime? JValueToDateTime(JValue jValue)
        {
            switch (jValue.Type)
            {
                case JTokenType.Date:
                    return jValue.Value<DateTime>().ToUniversalTime();
                case JTokenType.String:
                    return DateTime.Parse(jValue.Value<string>()).ToUniversalTime();
                default:
                    var jvalueDouble = ParseDoubleFromJValue(jValue);
                    if (jvalueDouble.HasValue)
                    {
                        return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(jvalueDouble.Value);
                    }
                    break;
            }
            return null;
        }

        public static SemVersion JValueToSemVer(JValue jValue)
        {
            if (jValue.Type == JTokenType.String)
            {
                try
                {
                    return SemVersion.Parse(jValue.Value<String>(), strict: false);
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }
            return null;
        }
    }
}

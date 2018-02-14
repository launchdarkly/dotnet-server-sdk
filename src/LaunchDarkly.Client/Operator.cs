using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

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

                int comparison;
                DateTime? uDateTime;
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

                        if (TryCompareNumericValues(uValue, cValue, out comparison))
                        {
                            return comparison == 0;
                        }
                        break;
                    case "endsWith":
                        if (uValue.Type.Equals(JTokenType.String) && cValue.Type.Equals(JTokenType.String))
                        {
                            return uValue.Value<string>().EndsWith(cValue.Value<string>());
                        }
                        break;
                    case "startsWith":
                        if (uValue.Type.Equals(JTokenType.String) && cValue.Type.Equals(JTokenType.String))
                        {
                            return uValue.Value<string>().StartsWith(cValue.Value<string>());
                        }
                        break;
                    case "matches":
                        if (uValue.Type.Equals(JTokenType.String) && cValue.Type.Equals(JTokenType.String))
                        {
                            var regex = new Regex(cValue.Value<string>());
                            return regex.IsMatch(uValue.Value<string>());
                        }
                        break;
                    case "contains":
                        if (uValue.Type.Equals(JTokenType.String) && cValue.Type.Equals(JTokenType.String))
                        {
                            return uValue.Value<string>().Contains(cValue.Value<string>());
                        }
                        break;
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
                        uDateTime = JValueToDateTime(uValue);
                        if (uDateTime.HasValue)
                        {
                            var cDateTime = JValueToDateTime(cValue);
                            if (cDateTime.HasValue)
                            {
                                return DateTime.Compare(uDateTime.Value, cDateTime.Value) < 0;
                            }
                        }
                        break;
                    case "after":
                        uDateTime = JValueToDateTime(uValue);
                        if (uDateTime.HasValue)
                        {
                            var cDateTime = JValueToDateTime(cValue);
                            if (cDateTime.HasValue)
                            {
                                return DateTime.Compare(uDateTime.Value, cDateTime.Value) > 0;
                            }
                        }
                        break;
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

        private static bool TryCompareNumericValues(JValue x, JValue y, out int result)
        {
            if (!IsNumericValue(x) || !IsNumericValue(y))
            {
                result = default(int);
                return false;
            }

            result = x.CompareTo(y);
            return true;
        }

        private static bool IsNumericValue(JValue jValue)
        {
            return (jValue.Type.Equals(JTokenType.Float) || jValue.Type.Equals(JTokenType.Integer));
        }

        private static double? ParseDoubleFromJValue(JValue jValue)
        {
            if (IsNumericValue(jValue))
            {
                return (double) jValue;
            }
            return null;
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
    }
}

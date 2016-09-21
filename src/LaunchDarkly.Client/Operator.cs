using System;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace LaunchDarkly.Client
{
    static class Operator
    {
        private static readonly ILogger Logger = LdLogger.CreateLogger("Operator");

        internal static bool Apply(string op, JValue uValue, JValue cValue)
        {
            try
            {
                if (uValue == null || cValue == null)
                    return false;

                double? uDouble;
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

                        uDouble = ParseDoubleFromJValue(uValue);
                        if (uDouble.HasValue)
                        {
                            var cDouble = ParseDoubleFromJValue(cValue);
                            {
                                if (cDouble.HasValue)
                                {
                                    if (uDouble.Value.Equals(cDouble.Value)) return true;
                                }
                            }
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
                        uDouble = ParseDoubleFromJValue(uValue);
                        if (uDouble.HasValue)
                        {
                            var cDouble = ParseDoubleFromJValue(cValue);
                            {
                                if (cDouble.HasValue)
                                {
                                    if (uDouble.Value < cDouble.Value) return true;
                                }
                            }
                        }
                        break;
                    case "lessThanOrEqual":
                        uDouble = ParseDoubleFromJValue(uValue);
                        if (uDouble.HasValue)
                        {
                            var cDouble = ParseDoubleFromJValue(cValue);
                            {
                                if (cDouble.HasValue)
                                {
                                    if (uDouble.Value <= cDouble.Value) return true;
                                }
                            }
                        }
                        break;
                    case "greaterThan":
                        uDouble = ParseDoubleFromJValue(uValue);
                        if (uDouble.HasValue)
                        {
                            var cDouble = ParseDoubleFromJValue(cValue);
                            {
                                if (cDouble.HasValue)
                                {
                                    if (uDouble.Value > cDouble.Value) return true;
                                }
                            }
                        }
                        break;
                    case "greaterThanOrEqual":
                        uDouble = ParseDoubleFromJValue(uValue);
                        if (uDouble.HasValue)
                        {
                            var cDouble = ParseDoubleFromJValue(cValue);
                            {
                                if (cDouble.HasValue)
                                {
                                    if (uDouble.Value >= cDouble.Value) return true;
                                }
                            }
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
                Logger.LogDebug(
                    String.Format(
                        "Got a possibly expected exception when applying operator: {0} to user Value: {1} and feature flag value: {2}. Exception message: {3}",
                        op, uValue, cValue, e.Message));
            }
            return false;
        }

        private static double? ParseDoubleFromJValue(JValue jValue)
        {
            if (jValue.Type.Equals(JTokenType.Float) || jValue.Type.Equals(JTokenType.Integer))
            {
                return (double)jValue;
            }
            return null;
        }

        private static DateTime? JValueToDateTime(JValue jValue)
        {
            switch (jValue.Type)
            {
                case JTokenType.Date:
                    return jValue.Value<DateTime>();
                case JTokenType.String:
                    return XmlConvert.ToDateTime(jValue.Value<string>(), XmlDateTimeSerializationMode.Utc);
                default:
                    var jvalueDouble = ParseDoubleFromJValue(jValue);
                    if (jvalueDouble.HasValue)
                    {
                        return new DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(jvalueDouble.Value);
                    }
                    break;
            }
            return null;
        }
    }
}
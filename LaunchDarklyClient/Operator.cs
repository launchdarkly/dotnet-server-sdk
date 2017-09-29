using System;
using System.Text.RegularExpressions;
using Common.Logging;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient
{
	public static class Operator
	{
		private static readonly ILog log = LogManager.GetLogger("Operator");

		public static bool Apply(string op, JValue uValue, JValue cValue)
		{
			try
			{
				log.Trace($"Start {nameof(Apply)}");

				try
				{
					if (uValue == null || cValue == null)
					{
						return false;
					}

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
								double? cDouble = ParseDoubleFromJValue(cValue);
								{
									if (cDouble.HasValue)
									{
										if (uDouble.Value.Equals(cDouble.Value))
										{
											return true;
										}
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
								Regex regex = new Regex(cValue.Value<string>());
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
								double? cDouble = ParseDoubleFromJValue(cValue);
								{
									if (cDouble.HasValue)
									{
										if (uDouble.Value < cDouble.Value)
										{
											return true;
										}
									}
								}
							}
							break;
						case "lessThanOrEqual":
							uDouble = ParseDoubleFromJValue(uValue);
							if (uDouble.HasValue)
							{
								double? cDouble = ParseDoubleFromJValue(cValue);
								{
									if (cDouble.HasValue)
									{
										if (uDouble.Value <= cDouble.Value)
										{
											return true;
										}
									}
								}
							}
							break;
						case "greaterThan":
							uDouble = ParseDoubleFromJValue(uValue);
							if (uDouble.HasValue)
							{
								double? cDouble = ParseDoubleFromJValue(cValue);
								{
									if (cDouble.HasValue)
									{
										if (uDouble.Value > cDouble.Value)
										{
											return true;
										}
									}
								}
							}
							break;
						case "greaterThanOrEqual":
							uDouble = ParseDoubleFromJValue(uValue);
							if (uDouble.HasValue)
							{
								double? cDouble = ParseDoubleFromJValue(cValue);
								{
									if (cDouble.HasValue)
									{
										if (uDouble.Value >= cDouble.Value)
										{
											return true;
										}
									}
								}
							}
							break;
						case "before":
							uDateTime = JValueToDateTime(uValue);
							if (uDateTime.HasValue)
							{
								DateTime? cDateTime = JValueToDateTime(cValue);
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
								DateTime? cDateTime = JValueToDateTime(cValue);
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
					log.Debug($"Got a possibly expected exception when applying operator: {op} to user Value: {uValue} and feature flag value: {cValue}. Exception message: {e.Message}");
				}
				return false;
			}
			finally
			{
				log.Trace($"End {nameof(Apply)}");
			}
		}

		private static double? ParseDoubleFromJValue(JValue jValue)
		{
			try
			{
				log.Trace($"Start {nameof(ParseDoubleFromJValue)}");

				if (jValue.Type.Equals(JTokenType.Float) || jValue.Type.Equals(JTokenType.Integer))
				{
					return (double) jValue;
				}
				return null;
			}
			finally
			{
				log.Trace($"End {nameof(ParseDoubleFromJValue)}");
			}
		}

		//Visible for testing
		public static DateTime? JValueToDateTime(JValue jValue)
		{
			try
			{
				log.Trace($"Start {nameof(JValueToDateTime)}");

				switch (jValue.Type)
				{
					case JTokenType.Date:
						return jValue.Value<DateTime>().ToUniversalTime();
					case JTokenType.String:
						return DateTime.Parse(jValue.Value<string>()).ToUniversalTime();
					default:
						double? jvalueDouble = ParseDoubleFromJValue(jValue);

						if (jvalueDouble.HasValue)
						{
							return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(jvalueDouble.Value);
						}
						break;
				}
				return null;
				
			}
			finally
			{
				log.Trace($"End {nameof(JValueToDateTime)}");
			}
		}
	}
}
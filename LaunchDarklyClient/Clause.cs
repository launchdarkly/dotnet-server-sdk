using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient
{
	internal class Clause
	{
		private static readonly ILog log = LogManager.GetLogger<Clause>();

		[JsonConstructor]
		internal Clause(string attribute, string op, List<JValue> values, bool negate)
		{
			try
			{
				log.Trace($"Start constructor {nameof(Clause)}(string, string, List<JValue>, bool)");

				Attribute = attribute;
				Op = op;
				Values = values;
				Negate = negate;
				
			}
			finally
			{
				log.Trace($"End constructor {nameof(Clause)}(string, string, List<JValue>, bool)");
			}
		}

		internal string Attribute {get;}
		internal string Op {get;}
		internal List<JValue> Values {get;}
		internal bool Negate {get;}

		internal bool MatchesUser(User user)
		{
			try
			{
				log.Trace($"Start {nameof(MatchesUser)}");

				JToken userValue = user.GetValueForEvaluation(Attribute);
				if (userValue == null)
				{
					return false;
				}

				if (userValue is JArray)
				{
					JArray array = userValue as JArray;

					foreach (JToken element in array)
					{
						if (!(element is JValue))
						{
							log.Error($"Invalid custom attribute value in user object: {element}");
							return false;
						}
						if (MatchAny(element as JValue))
						{
							return MaybeNegate(true);
						}
					}
					return MaybeNegate(false);
				}
				else if (userValue is JValue)
				{
					return MaybeNegate(MatchAny(userValue as JValue));
				}
				log.Warn($"Got unexpected user attribute type: {userValue.Type} for user key: {user.Key} and attribute: {Attribute}");
				return false;
			}
			finally
			{
				log.Trace($"End {nameof(MatchesUser)}");
			}
		}

		private bool MatchAny(JValue userValue)
		{
			try
			{
				log.Trace($"Start {nameof(MatchAny)}");

				foreach (JValue v in Values)
				{
					if (Operator.Apply(Op, userValue, v))
					{
						return true;
					}
				}
				return false;
			}
			finally
			{
				log.Trace($"End {nameof(MatchAny)}");
			}
		}

		private bool MaybeNegate(bool b)
		{
			try
			{
				log.Trace($"Start {nameof(MaybeNegate)}");

				if (Negate)
				{
					return !b;
				}
				else
				{
					return b;
				}
			}
			finally
			{
				log.Trace($"End {nameof(MaybeNegate)}");
			}
		}
	}
}
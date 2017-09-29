using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;

namespace LaunchDarklyClient
{
	internal class Rule : VariationOrRollout
	{
		private static readonly ILog log = LogManager.GetLogger<Rule>();

		[JsonConstructor]
		internal Rule(int? variation, Rollout rollout, List<Clause> clauses) : base(variation, rollout)
		{
			try
			{
				log.Trace($"Start constructor {nameof(Rule)}(int?, Rollout, List<Clause>)");
				Clauses = clauses;
			}
			finally
			{
				log.Trace($"End constructor {nameof(Rule)}(int?, Rollout, List<Clause>)");
			}
		}

		internal List<Clause> Clauses {get;}

		internal bool MatchesUser(User user)
		{
			try
			{
				log.Trace($"Start {nameof(MatchesUser)}");

				foreach (Clause clause in Clauses)
				{
					if (!clause.MatchesUser(user))
					{
						return false;
					}
				}
				return true;
			}
			finally
			{
				log.Trace($"End {nameof(MatchesUser)}");
			}
		}
	}
}
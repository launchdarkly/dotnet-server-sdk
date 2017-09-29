using System.Collections.Generic;
using Common.Logging;
using LaunchDarklyClient.Events;
using LaunchDarklyClient.Exceptions;
using LaunchDarklyClient.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LaunchDarklyClient
{
	public class FeatureFlag
	{
		private static readonly ILog log = LogManager.GetLogger<FeatureFlag>();

		[JsonConstructor]
		internal FeatureFlag(string key, int version, bool on, List<Prerequisite> prerequisites, string salt,
			List<Target> targets, List<Rule> rules, VariationOrRollout fallthrough, int? offVariation,
			List<JToken> variations,
			bool deleted)
		{
			try
			{
				log.Trace($"Start internal constructor {nameof(FeatureFlag)}(string, int, bool, List<Prerequisite>, string, List<Target>, List<Rule>, VariationOrRollout, int?, List<JToken>)");

				Key = key;
				Version = version;
				On = on;
				Prerequisites = prerequisites;
				Salt = salt;
				Targets = targets;
				Rules = rules;
				Fallthrough = fallthrough;
				OffVariation = offVariation;
				Variations = variations;
				Deleted = deleted;
			}
			finally
			{
				log.Trace($"End internal constructor {nameof(FeatureFlag)}(string, int, bool, List<Prerequisite>, string, List<Target>, List<Rule>, VariationOrRollout, int?, List<JToken>)");
			}
		}

		internal FeatureFlag()
		{
			try
			{
				log.Trace($"Start internal constructor {nameof(FeatureFlag)}()");
			}
			finally
			{
				log.Trace($"End internal constructor {nameof(FeatureFlag)}()");
			}
		}

		internal string Key {get;}
		internal int Version {get; set;}
		internal bool On {get;}
		internal List<Prerequisite> Prerequisites {get;}
		internal string Salt {get;}
		internal List<Target> Targets {get;}
		internal List<Rule> Rules {get;}
		internal VariationOrRollout Fallthrough {get;}
		internal int? OffVariation {get;}
		internal List<JToken> Variations {get;}
		internal bool Deleted {get; set;}

		internal JToken OffVariationValue
		{
			get
			{
				try
				{
					log.Trace($"Start {nameof(OffVariationValue)}.get");

					if (!OffVariation.HasValue)
					{
						return null;
					}

					if (OffVariation.Value >= Variations.Count)
					{
						throw new EvaluationException("Invalid off variation index");
					}

					return Variations[OffVariation.Value];
				}
				finally
				{
					log.Trace($"End {nameof(OffVariationValue)}.get");
				}
			}
		}

		internal EvalResult Evaluate(User user, IFeatureStore featureStore)
		{
			try
			{
				log.Trace($"Start {nameof(Evaluate)}");

				IList<FeatureRequestEvent> prereqEvents = new List<FeatureRequestEvent>();
				EvalResult evalResult = new EvalResult(null, prereqEvents);
				if (user?.Key == null)
				{
					log.Warn("User or user key is null when evaluating flag: " + Key + " returning null");
					return evalResult;
				}

				if (On)
				{
					evalResult.Result = Evaluate(user, featureStore, prereqEvents);
					if (evalResult.Result != null)
					{
						return evalResult;
					}
				}
				evalResult.Result = OffVariationValue;
				return evalResult;
			}
			finally
			{
				log.Trace($"End {nameof(Evaluate)}");
			}
		}

		// Returning either a nil EvalResult or EvalResult.value indicates prereq failure/error.
		private JToken Evaluate(User user, IFeatureStore featureStore, IList<FeatureRequestEvent> events)
		{
			try
			{
				log.Trace($"Start {nameof(Evaluate)}");

				bool prereqOk = true;
				if (Prerequisites != null)
				{
					foreach (Prerequisite prereq in Prerequisites)
					{
						FeatureFlag prereqFeatureFlag = featureStore.Get(prereq.Key);
						JToken prereqEvalResult = null;
						if (prereqFeatureFlag == null)
						{
							log.Error($"Could not retrieve prerequisite flag: {prereq.Key} when evaluating: {Key}");
							return null;
						}
						else if (prereqFeatureFlag.On)
						{
							prereqEvalResult = prereqFeatureFlag.Evaluate(user, featureStore, events);
							try
							{
								JToken variation = prereqFeatureFlag.GetVariation(prereq.Variation);
								if (prereqEvalResult == null || variation == null || !prereqEvalResult.Equals(variation))
								{
									prereqOk = false;
								}
							}
							catch (EvaluationException e)
							{
								log.Warn("Error evaluating prerequisites: " + e.Message, e);
								prereqOk = false;
							}
						}
						else
						{
							prereqOk = false;
						}
						//We don't short circuit and also send events for each prereq.
						events.Add(new FeatureRequestEvent(prereqFeatureFlag.Key, user, prereqEvalResult, null, prereqFeatureFlag.Version, prereq.Key));
					}
				}

				return prereqOk ? GetVariation(EvaluateIndex(user)) : null;
			}
			finally
			{
				log.Trace($"End {nameof(Evaluate)}");
			}
		}

		private int? EvaluateIndex(User user)
		{
			try
			{
				log.Trace($"Start {nameof(EvaluateIndex)}");

				// Check to see if targets match
				foreach (Target target in Targets)
				{
					foreach (string v in target.Values)
					{
						if (v.Equals(user.Key))
						{
							return target.Variation;
						}
					}
				}

				// Now walk through the rules and see if any match
				foreach (Rule rule in Rules)
				{
					if (rule.MatchesUser(user))
					{
						return rule.VariationIndexForUser(user, Key, Salt);
					}
				}

				// Walk through the fallthrough and see if it matches
				return Fallthrough.VariationIndexForUser(user, Key, Salt);
			}
			finally
			{
				log.Trace($"End {nameof(EvaluateIndex)}");
			}
		}

		private JToken GetVariation(int? index)
		{
			try
			{
				log.Trace($"Start {nameof(GetVariation)}");

				// If the supplied index is null, then rules didn't match, and we want to return
				// the off variation
				if (index == null)
				{
					return null;
				}
				// If the index doesn't refer to a valid variation, that's an unexpected exception and we will
				// return the default variation
				else if (index >= Variations.Count)
				{
					throw new EvaluationException("Invalid index");
				}
				else
				{
					return Variations[index.Value];
				}
			}
			finally
			{
				log.Trace($"End {nameof(GetVariation)}");
			}
		}

		internal struct EvalResult
		{
			internal JToken Result;
			internal readonly IList<FeatureRequestEvent> PrerequisiteEvents;

			internal EvalResult(JToken result, IList<FeatureRequestEvent> events) : this()
			{
				try
				{
					log.Trace($"Start constructor {nameof(EvalResult)}(JToken, IList<FeatureRequestEvent)");

					Result = result;
					PrerequisiteEvents = events;
				}
				finally
				{
					log.Trace($"End constructor {nameof(EvalResult)}(JToken, IList<FeatureRequestEvent)");
				}
			}
		}
	}
}
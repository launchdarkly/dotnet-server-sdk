using System;
using System.Collections.Generic;
using Common.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LaunchDarkly.Common;

namespace LaunchDarkly.Client
{
    internal class FeatureFlag : IVersionedData, IFlagEventProperties
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FeatureFlag));

        [JsonProperty(PropertyName = "key")]
        public string Key { get; private set; }
        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }
        [JsonProperty(PropertyName = "on")]
        internal bool On { get; private set; }
        [JsonProperty(PropertyName = "prerequisites")]
        internal List<Prerequisite> Prerequisites { get; private set; }
        [JsonProperty(PropertyName = "salt")]
        internal string Salt { get; private set; }
        [JsonProperty(PropertyName = "targets")]
        internal List<Target> Targets { get; private set; }
        [JsonProperty(PropertyName = "rules")]
        internal List<Rule> Rules { get; private set; }
        [JsonProperty(PropertyName = "fallthrough")]
        internal VariationOrRollout Fallthrough { get; private set; }
        [JsonProperty(PropertyName = "offVariation")]
        internal int? OffVariation { get; private set; }
        [JsonProperty(PropertyName = "variations")]
        internal List<JToken> Variations { get; private set; }
        [JsonProperty(PropertyName = "trackEvents")]
        public bool TrackEvents { get; private set; }
        [JsonProperty(PropertyName = "debugEventsUntilDate")]
        public long? DebugEventsUntilDate { get; private set; }
        [JsonProperty(PropertyName = "deleted")]
        public bool Deleted { get; set; }

        [JsonConstructor]
        internal FeatureFlag(string key, int version, bool on, List<Prerequisite> prerequisites, string salt,
            List<Target> targets, List<Rule> rules, VariationOrRollout fallthrough, int? offVariation,
            List<JToken> variations, bool trackEvents, long? debugEventsUntilDate,
            bool deleted)
        {
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
            TrackEvents = trackEvents;
            DebugEventsUntilDate = debugEventsUntilDate;
            Deleted = deleted;
        }

        internal FeatureFlag()
        {
        }

        internal struct EvalResult
        {
            internal EvaluationDetail<JToken> Result;
            internal readonly IList<FeatureRequestEvent> PrerequisiteEvents;
            
            internal EvalResult(EvaluationDetail<JToken> result, IList<FeatureRequestEvent> events) : this()
            {
                Result = result;
                PrerequisiteEvents = events;
            }
        }
        
        internal EvalResult Evaluate(User user, IFeatureStore featureStore, EventFactory eventFactory)
        {
            IList<FeatureRequestEvent> prereqEvents = new List<FeatureRequestEvent>();
            if (user == null || user.Key == null)
            {
                Log.WarnFormat("User or user key is null when evaluating flag: {0} returning null",
                    Key);

                return new EvalResult(
                    new EvaluationDetail<JToken>(null, null, new EvaluationReason.Error(EvaluationErrorKind.USER_NOT_SPECIFIED)),
                    prereqEvents);
            }

            if (On)
            {
                var details = Evaluate(user, featureStore, prereqEvents, eventFactory);
                return new EvalResult(details, prereqEvents);
            }

            return new EvalResult(GetOffValue(EvaluationReason.Off.Instance), prereqEvents);
        }

        private EvaluationDetail<JToken> Evaluate(User user, IFeatureStore featureStore, IList<FeatureRequestEvent> events,
            EventFactory eventFactory)
        {
            var prereqFailureReason = CheckPrerequisites(user, featureStore, events, eventFactory);
            if (prereqFailureReason != null)
            {
                return GetOffValue(prereqFailureReason);
            }
            
            // Check to see if targets match
            if (Targets != null)
            {
                foreach (var target in Targets)
                {
                    foreach (var v in target.Values)
                    {
                        if (user.Key == v)
                        {
                            return GetVariation(target.Variation, EvaluationReason.TargetMatch.Instance);
                        }
                    }
                }
            }
            // Now walk through the rules and see if any match
            if (Rules != null)
            {
                for (int i = 0; i < Rules.Count; i++)
                {
                    Rule rule = Rules[i];
                    if (rule.MatchesUser(user, featureStore))
                    {
                        return GetValueForVariationOrRollout(rule, user,
                            new EvaluationReason.RuleMatch(i, rule.Id));
                    }
                }
            }
            // Walk through the fallthrough and see if it matches
            return GetValueForVariationOrRollout(Fallthrough, user, EvaluationReason.Fallthrough.Instance);
        }

        // Checks prerequisites if any; returns null if successful, or an EvaluationReason if we have to
        // short-circuit due to a prerequisite failure.
        private EvaluationReason CheckPrerequisites(User user, IFeatureStore featureStore, IList<FeatureRequestEvent> events,
            EventFactory eventFactory)
        {
            if (Prerequisites == null || Prerequisites.Count == 0)
            {
                return null;
            }
            foreach (var prereq in Prerequisites)
            {
                var prereqOk = true;
                var prereqFeatureFlag = featureStore.Get(VersionedDataKind.Features, prereq.Key);
                EvaluationDetail<JToken> prereqEvalResult = null;
                if (prereqFeatureFlag == null)
                {
                    Log.ErrorFormat("Could not retrieve prerequisite flag \"{0}\" when evaluating \"{1}\"",
                        prereq.Key, Key);
                    prereqOk = false;
                }
                else if (prereqFeatureFlag.On)
                {
                    prereqEvalResult = prereqFeatureFlag.Evaluate(user, featureStore, events, eventFactory);
                    if (prereqEvalResult.VariationIndex == null || prereqEvalResult.VariationIndex.Value != prereq.Variation)
                    {
                        prereqOk = false;
                    }
                }
                else
                {
                    prereqOk = false;
                }
                if (prereqFeatureFlag != null)
                {
                    events.Add(eventFactory.NewPrerequisiteFeatureRequestEvent(prereqFeatureFlag, user,
                        prereqEvalResult, this));
                }
                if (!prereqOk)
                {
                    return new EvaluationReason.PrerequisiteFailed(prereq.Key);
                }
            }
            return null;
        }
        
        internal EvaluationDetail<JToken> ErrorResult(EvaluationErrorKind kind)
        {
            return new EvaluationDetail<JToken>(null, null, new EvaluationReason.Error(kind));
        }

        internal EvaluationDetail<JToken> GetVariation(int variation, EvaluationReason reason)
        {
            if (variation < 0 || variation >= Variations.Count)
            {
                Log.ErrorFormat("Data inconsistency in feature flag \"{0}\": invalid variation index", Key);
                return ErrorResult(EvaluationErrorKind.MALFORMED_FLAG);
            }
            return new EvaluationDetail<JToken>(Variations[variation], variation, reason);
        }

        internal EvaluationDetail<JToken> GetOffValue(EvaluationReason reason)
        {
            if (OffVariation == null) // off variation unspecified - return default value
            {
                return new EvaluationDetail<JToken>(null, null, reason);
            }
            return GetVariation(OffVariation.Value, reason);
        }

        internal EvaluationDetail<JToken> GetValueForVariationOrRollout(VariationOrRollout vr,
            User user, EvaluationReason reason)
        {
            var index = vr.VariationIndexForUser(user, Key, Salt);
            if (index == null)
            {
                Log.ErrorFormat("Data inconsistency in feature flag \"{0}\": variation/rollout object with no variation or rollout", Key);
                return ErrorResult(EvaluationErrorKind.MALFORMED_FLAG);
            }
            return GetVariation(index.Value, reason);
        }
    }

    internal class Rollout
    {
        [JsonProperty(PropertyName = "variations")]
        internal List<WeightedVariation> Variations { get; private set; }
        [JsonProperty(PropertyName = "bucketBy")]
        internal string BucketBy { get; private set; }

        [JsonConstructor]
        internal Rollout(List<WeightedVariation> variations, string bucketBy)
        {
            Variations = variations;
            BucketBy = bucketBy;
        }
    }

    internal class WeightedVariation
    {
        [JsonProperty(PropertyName = "variation")]
        internal int Variation { get; private set; }
        [JsonProperty(PropertyName = "weight")]
        internal int Weight { get; private set; }

        [JsonConstructor]
        internal WeightedVariation(int variation, int weight)
        {
            Variation = variation;
            Weight = weight;
        }
    }

    internal class Target
    {
        [JsonProperty(PropertyName = "values")]
        internal List<string> Values { get; private set; }
        [JsonProperty(PropertyName = "variation")]
        internal int Variation { get; private set; }

        [JsonConstructor]
        internal Target(List<string> values, int variation)
        {
            Values = values;
            Variation = variation;
        }
    }

    internal class Prerequisite
    {
        [JsonProperty(PropertyName = "key")]
        internal string Key { get; private set; }
        [JsonProperty(PropertyName = "variation")]
        internal int Variation { get; private set; }

        [JsonConstructor]
        internal Prerequisite(string key, int variation)
        {
            Key = key;
            Variation = variation;
        }
    }

    class EvaluationException : Exception
    {
        public EvaluationException(string message)
            : base(message)
        {
        }
    }
}
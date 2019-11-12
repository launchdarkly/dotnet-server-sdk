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
        [JsonProperty(PropertyName = "trackEventsFallthrough")]
        public bool TrackEventsFallthrough { get; private set; }
        [JsonProperty(PropertyName = "debugEventsUntilDate")]
        public long? DebugEventsUntilDate { get; private set; }
        [JsonProperty(PropertyName = "deleted")]
        public bool Deleted { get; set; }
        [JsonProperty(PropertyName = "clientSide")]
        public bool ClientSide { get; set; }

        [JsonConstructor]
        internal FeatureFlag(string key, int version, bool on, List<Prerequisite> prerequisites, string salt,
            List<Target> targets, List<Rule> rules, VariationOrRollout fallthrough, int? offVariation,
            List<JToken> variations, bool trackEvents, bool trackEventsFallthrough, long? debugEventsUntilDate,
            bool deleted, bool clientSide)
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
            TrackEventsFallthrough = trackEventsFallthrough;
            DebugEventsUntilDate = debugEventsUntilDate;
            Deleted = deleted;
            ClientSide = clientSide;
        }

        internal FeatureFlag()
        {
        }

        internal FeatureFlag(string key, int version, bool deleted)
        {
            Key = key;
            Version = version;
            Deleted = deleted;
        }
        
        internal struct EvalResult
        {
            internal EvaluationDetail<LdValue> Result;
            internal readonly IList<FeatureRequestEvent> PrerequisiteEvents;
            
            internal EvalResult(EvaluationDetail<LdValue> result, IList<FeatureRequestEvent> events) : this()
            {
                Result = result;
                PrerequisiteEvents = events;
            }
        }
        
        int IFlagEventProperties.EventVersion => Version;

        // This method is called by EventFactory to determine if extra tracking should be
        // enabled for an event, based on the evaluation reason.
        bool IFlagEventProperties.IsExperiment(EvaluationReason reason)
        {
            switch (reason.Kind)
            {
                case EvaluationReasonKind.FALLTHROUGH:
                    return TrackEventsFallthrough;
                case EvaluationReasonKind.RULE_MATCH:
                    return reason.RuleIndex >= 0 && Rules != null && reason.RuleIndex < Rules.Count &&
                        Rules[reason.RuleIndex].TrackEvents;
            }
            return false;
        }

        internal EvalResult Evaluate(User user, IFeatureStore featureStore, EventFactory eventFactory)
        {
            IList<FeatureRequestEvent> prereqEvents = new List<FeatureRequestEvent>();
            if (user == null || user.Key == null)
            {
                Log.WarnFormat("User or user key is null when evaluating flag: {0} returning null",
                    Key);

                return new EvalResult(
                    new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.ErrorReason(EvaluationErrorKind.USER_NOT_SPECIFIED)),
                    prereqEvents);
            }
            var details = Evaluate(user, featureStore, prereqEvents, eventFactory);
            return new EvalResult(details, prereqEvents);
        }

        private EvaluationDetail<LdValue> Evaluate(User user, IFeatureStore featureStore, IList<FeatureRequestEvent> events,
            EventFactory eventFactory)
        {
            if (!On)
            {
                return GetOffValue(EvaluationReason.OffReason);
            }

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
                            return GetVariation(target.Variation, EvaluationReason.TargetMatchReason);
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
                            EvaluationReason.RuleMatchReason(i, rule.Id));
                    }
                }
            }
            // Walk through the fallthrough and see if it matches
            return GetValueForVariationOrRollout(Fallthrough, user, EvaluationReason.FallthroughReason);
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
                if (prereqFeatureFlag == null)
                {
                    Log.ErrorFormat("Could not retrieve prerequisite flag \"{0}\" when evaluating \"{1}\"",
                        prereq.Key, Key);
                    prereqOk = false;
                }
                else
                {
                    var prereqEvalResult = prereqFeatureFlag.Evaluate(user, featureStore, events, eventFactory);
                    // Note that if the prerequisite flag is off, we don't consider it a match no matter
                    // what its off variation was. But we still need to evaluate it in order to generate
                    // an event.
                    if (!prereqFeatureFlag.On || prereqEvalResult.VariationIndex == null || prereqEvalResult.VariationIndex.Value != prereq.Variation)
                    {
                        prereqOk = false;
                    }
                    events.Add(eventFactory.NewPrerequisiteFeatureRequestEvent(prereqFeatureFlag, user,
                        prereqEvalResult, this));
                }
                if (!prereqOk)
                {
                    return EvaluationReason.PrerequisiteFailedReason(prereq.Key);
                }
            }
            return null;
        }
        
        internal EvaluationDetail<LdValue> ErrorResult(EvaluationErrorKind kind)
        {
            return new EvaluationDetail<LdValue>(LdValue.Null, null, EvaluationReason.ErrorReason(kind));
        }

        internal EvaluationDetail<LdValue> GetVariation(int variation, EvaluationReason reason)
        {
            if (variation < 0 || variation >= Variations.Count)
            {
                Log.ErrorFormat("Data inconsistency in feature flag \"{0}\": invalid variation index", Key);
                return ErrorResult(EvaluationErrorKind.MALFORMED_FLAG);
            }
            return new EvaluationDetail<LdValue>(LdValue.FromSafeValue(Variations[variation]), variation, reason);
        }

        internal EvaluationDetail<LdValue> GetOffValue(EvaluationReason reason)
        {
            if (OffVariation == null) // off variation unspecified - return default value
            {
                return new EvaluationDetail<LdValue>(LdValue.Null, null, reason);
            }
            return GetVariation(OffVariation.Value, reason);
        }

        internal EvaluationDetail<LdValue> GetValueForVariationOrRollout(VariationOrRollout vr,
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
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

        public string Key { get; private set; }
        public int Version { get; set; }
        internal bool On { get; private set; }
        internal List<Prerequisite> Prerequisites { get; private set; }
        internal string Salt { get; private set; }
        internal List<Target> Targets { get; private set; }
        internal List<Rule> Rules { get; private set; }
        internal VariationOrRollout Fallthrough { get; private set; }
        internal int? OffVariation { get; private set; }
        internal List<JToken> Variations { get; private set; }
        public bool TrackEvents { get; private set; }
        public long? DebugEventsUntilDate { get; private set; }
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
            internal int? Variation;
            internal JToken Result;
            internal readonly IList<FeatureRequestEvent> PrerequisiteEvents;
            
            internal EvalResult(int? variation, JToken result, IList<FeatureRequestEvent> events) : this()
            {
                Variation = variation;
                Result = result;
                PrerequisiteEvents = events;
            }
        }
        
        internal EvalResult Evaluate(User user, IFeatureStore featureStore, EventFactory eventFactory)
        {
            IList<FeatureRequestEvent> prereqEvents = new List<FeatureRequestEvent>();
            EvalResult evalResult = new EvalResult(null, null, prereqEvents);
            if (user == null || user.Key == null)
            {
                Log.WarnFormat("User or user key is null when evaluating flag: {0} returning null",
                    Key);

                return evalResult;
            }

            if (On)
            {
                evalResult = Evaluate(user, featureStore, prereqEvents, eventFactory);
                if (evalResult.Result != null)
                {
                    return evalResult;
                }
            }
            evalResult.Variation = OffVariation;
            evalResult.Result = OffVariationValue;
            return evalResult;
        }

        // Returning either a nil EvalResult or EvalResult.value indicates prereq failure/error.
        private EvalResult Evaluate(User user, IFeatureStore featureStore, IList<FeatureRequestEvent> events,
            EventFactory eventFactory)
        {
            var prereqOk = true;
            if (Prerequisites != null)
            {
                foreach (var prereq in Prerequisites)
                {
                    var prereqFeatureFlag = featureStore.Get(VersionedDataKind.Features, prereq.Key);
                    EvalResult prereqEvalResult = new EvalResult(null, null, events);
                    if (prereqFeatureFlag == null)
                    {
                        Log.ErrorFormat("Could not retrieve prerequisite flag: {0} when evaluating: {1}",
                            prereq.Key,
                            Key);
                        return new EvalResult(null, null, events);
                    }
                    else if (prereqFeatureFlag.On)
                    {
                        prereqEvalResult = prereqFeatureFlag.Evaluate(user, featureStore, events, eventFactory);
                        try
                        {
                            if (prereqEvalResult.Variation != prereq.Variation)
                            {
                                prereqOk = false;
                            }
                        }
                        catch (EvaluationException e)
                        {
                            Log.WarnFormat("Error evaluating prerequisites: {0}",
                                e,
                                Util.ExceptionMessage(e));

                            prereqOk = false;
                        }
                    }
                    else
                    {
                        prereqOk = false;
                    }
                    //We don't short circuit and also send events for each prereq.
                    events.Add(eventFactory.NewPrerequisiteFeatureRequestEvent(prereqFeatureFlag,
                        user, null, prereqEvalResult.Result, this));
                }
            }
            if (prereqOk)
            {
                int? index = EvaluateIndex(user, featureStore);
                JToken result = GetVariation(index);
                return new EvalResult(index, result, events);
            }
            return new EvalResult(null, null, events);
        }
        
        private int? EvaluateIndex(User user, IFeatureStore store)
        {
            // Check to see if targets match
            foreach (var target in Targets)
            {
                foreach (var v in target.Values)
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
                if (rule.MatchesUser(user, store))
                {
                    return rule.VariationIndexForUser(user, Key, Salt);
                }
            }

            // Walk through the fallthrough and see if it matches
            return Fallthrough.VariationIndexForUser(user, Key, Salt);
        }

        private JToken GetVariation(int? index)
        {
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

        internal JToken OffVariationValue
        {
            get
            {
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
        }
    }

    class Rollout
    {
        internal List<WeightedVariation> Variations { get; private set; }
        internal string BucketBy { get; private set; }

        [JsonConstructor]
        internal Rollout(List<WeightedVariation> variations, string bucketBy)
        {
            Variations = variations;
            BucketBy = bucketBy;
        }
    }

    class WeightedVariation
    {
        internal int Variation { get; private set; }
        internal int Weight { get; private set; }

        [JsonConstructor]
        internal WeightedVariation(int variation, int weight)
        {
            Variation = variation;
            Weight = weight;
        }
    }

    class Target
    {
        internal List<string> Values { get; private set; }
        internal int Variation { get; private set; }

        [JsonConstructor]
        internal Target(List<string> values, int variation)
        {
            Values = values;
            Variation = variation;
        }
    }

    class Prerequisite
    {
        internal string Key { get; private set; }
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
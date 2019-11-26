using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LaunchDarkly.Sdk.Internal.Helpers;
using Newtonsoft.Json;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// A snapshot of the state of all feature flags with regard to a specific user. See
    /// calling <see cref="ILdClient.AllFlagsState(User, FlagsStateOption[])"/>.
    /// </summary>
    /// <remarks>
    /// Serializing this object to JSON using <c>JsonConvert.SerializeObject()</c> will produce the
    /// appropriate data structure for bootstrapping the LaunchDarkly JavaScript client.
    /// </remarks>
    [JsonConverter(typeof(FeatureFlagsStateConverter))]
    public class FeatureFlagsState
    {
        internal readonly bool _valid;
        internal readonly IDictionary<string, LdValue> _flagValues;
        internal readonly IDictionary<string, FlagMetadata> _flagMetadata;
        private volatile ImmutableDictionary<string, LdValue> _immutableValuesMap; // lazily created
        
        /// <summary>
        /// True if this object contains a valid snapshot of feature flag state, or false if the
        /// state could not be computed (for instance, because the client was offline or there was no user).
        /// </summary>
        public bool Valid => _valid;

        internal FeatureFlagsState(bool valid)
        {
            _valid = valid;
            _flagValues = new Dictionary<string, LdValue>();
            _flagMetadata = new Dictionary<string, FlagMetadata>();
        }

        internal FeatureFlagsState(bool valid, IDictionary<string, LdValue> values,
            IDictionary<string, FlagMetadata> metadata)
        {
            _valid = valid;
            _flagValues = values;
            _flagMetadata = metadata;
        }

        internal void AddFlag(FeatureFlag flag, LdValue value, int? variation, EvaluationReason? reason,
            bool detailsOnlyIfTracked)
        {
            _flagValues[flag.Key] = value;
            var meta = new FlagMetadata
            {
                Variation = variation,
                DebugEventsUntilDate = flag.DebugEventsUntilDate
            };
            if (!detailsOnlyIfTracked || flag.TrackEvents || flag.DebugEventsUntilDate != null)
            {
                meta.Version = flag.Version;
                meta.Reason = reason;
            }
            if (flag.TrackEvents)
            {
                meta.TrackEvents = true;
            }
            _flagMetadata[flag.Key] = meta;
        }
        
        /// <summary>
        /// Returns the value of an individual feature flag at the time the state was recorded.
        /// </summary>
        /// <param name="key">the feature flag key</param>
        /// <returns>the flag's JSON value; <see cref="LdValue.Null"/> if the flag returned
        /// the default value, or if there was no such flag</returns>
        public LdValue GetFlagValueJson(string key)
        {
            if (_flagValues.TryGetValue(key, out var value))
            {
                return value;
            }
            return LdValue.Null;
        }

        /// <summary>
        /// Returns the evaluation reason of an individual feature flag (as returned by
        /// <see cref="ILdClient.BoolVariation(string, User, bool)"/>, etc.) at the time the state
        /// was recorded.
        /// </summary>
        /// <param name="key">the feature flag key</param>
        /// <returns>the evaluation reason; null if reasons were not recorded, or if there was no
        /// such flag</returns>
        public EvaluationReason? GetFlagReason(string key)
        {
            if (_flagMetadata.TryGetValue(key, out var meta))
            {
                return meta.Reason;
            }
            return null;
        }
        
        /// <summary>
        /// Returns a dictionary of flag keys to flag values.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If a flag would have evaluated to the default value, its value will be
        /// <see cref="LdValue.Null"/>.
        /// </para>
        /// <para>
        /// Do not use this method if you are passing data to the front end to "bootstrap" the
        /// JavaScript client. Instead, serialize the <see cref="FeatureFlagsState"/> object to JSON
        /// using <c>JsonConvert.SerializeObject()</c>.
        /// </para>
        /// </remarks>
        /// <returns>a dictionary of flag keys to flag values</returns>
        public IReadOnlyDictionary<string, LdValue> ToValuesJsonMap()
        {
            // In the next major version, we will store the map this way in the first place so there will
            // be no conversion step.
            lock (this)
            {
                if (_immutableValuesMap is null)
                {
                    // There's a potential race condition here but the result is the same either way, so 
                    _immutableValuesMap = _flagValues.ToImmutableDictionary();
                }
                return _immutableValuesMap;
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object other)
        {
            if (other is FeatureFlagsState o)
            {
                return _valid == o._valid &&
                    DictionariesEqual(_flagValues, o._flagValues) &&
                    DictionariesEqual(_flagMetadata, o._flagMetadata);
            }
            return false;
        }

        /// <inheritdoc/>

        public override int GetHashCode()
        {
            return Util.Hash().With(_flagValues).With(_flagMetadata).With(_valid).Value;
        }
        
        private static bool DictionariesEqual<T, U>(IDictionary<T, U> d0, IDictionary<T, U> d1)
        {
            return d0.Count == d1.Count && d0.All(kv =>
                d1.TryGetValue(kv.Key, out var v) && kv.Value.Equals(v));
        }
    }

    internal class FlagMetadata
    {
        [JsonProperty(PropertyName = "variation", NullValueHandling = NullValueHandling.Ignore)]
        internal int? Variation { get; set; }
        [JsonProperty(PropertyName = "version", NullValueHandling = NullValueHandling.Ignore)]
        internal int? Version { get; set; }
        [JsonProperty(PropertyName = "trackEvents", NullValueHandling = NullValueHandling.Ignore)]
        internal bool? TrackEvents { get; set; }
        [JsonProperty(PropertyName = "debugEventsUntilDate", NullValueHandling = NullValueHandling.Ignore)]
        internal long? DebugEventsUntilDate { get; set; }
        [JsonProperty(PropertyName = "reason", NullValueHandling = NullValueHandling.Ignore)]
        internal EvaluationReason? Reason { get; set; }

        public override bool Equals(object other)
        {
            if (other is FlagMetadata o)
            {
                return Variation == o.Variation &&
                    Version == o.Version &&
                    TrackEvents == o.TrackEvents &&
                    DebugEventsUntilDate == o.DebugEventsUntilDate &&
                    Object.Equals(Reason, o.Reason);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Util.Hash().With(Variation).With(Version).With(TrackEvents).With(DebugEventsUntilDate).With(Reason).Value;
        }
    }

    internal class FeatureFlagsStateConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeatureFlagsState state)
            {
                writer.WriteStartObject();
                foreach (var entry in state._flagValues)
                {
                    writer.WritePropertyName(entry.Key);
                    serializer.Serialize(writer, entry.Value);
                }
                writer.WritePropertyName("$flagsState");
                writer.WriteStartObject();
                foreach (var entry in state._flagMetadata)
                {
                    writer.WritePropertyName(entry.Key);
                    serializer.Serialize(writer, entry.Value);
                }
                writer.WriteEnd();
                writer.WritePropertyName("$valid");
                writer.WriteValue(state._valid);
                writer.WriteEnd();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var valid = true;
            var flagValues = new Dictionary<string, LdValue>();
            var flagMetadata = new Dictionary<string, FlagMetadata>();
            // This is somewhat inefficient, compared to interacting with the JsonReader directly, but
            // it's much easier to write this way. Deserialization isn't a typical use case for this
            // class anyway.
            LdValue o = serializer.Deserialize<LdValue>(reader);
            foreach (var kv in o.AsDictionary(LdValue.Convert.Json))
            {
                if (kv.Key == "$flagsState")
                {
                    foreach (var prop1 in kv.Value.AsDictionary(LdValue.Convert.Json))
                    {
                        flagMetadata[prop1.Key] = JsonConvert.DeserializeObject<FlagMetadata>(prop1.Value.ToJsonString());
                    }
                }
                else if (kv.Key == "$valid")
                {
                    valid = kv.Value.AsBool;
                }
                else
                {
                    flagValues[kv.Key] = kv.Value;
                }
            }
            return new FeatureFlagsState(valid, flagValues, flagMetadata);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeatureFlagsState);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using LaunchDarkly.Sdk.Internal;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Internal.JsonConverterHelpers;
using static LaunchDarkly.Sdk.Json.LdJsonConverters;

namespace LaunchDarkly.Sdk.Server
{
    /// <summary>
    /// A snapshot of the state of all feature flags with regard to a specific user. See
    /// calling <see cref="ILdClient.AllFlagsState(Context, FlagsStateOption[])"/>.
    /// </summary>
    /// <remarks>
    /// Serializing this object to JSON using <c>System.Text.Json</c> or <see cref="LaunchDarkly.Sdk.Json.LdJsonSerialization"/>
    /// will produce the appropriate data structure for bootstrapping the LaunchDarkly JavaScript client.
    /// Using <c>Newtonsoft.Json</c> will not work correctly without special handling; see
    /// <see cref="LaunchDarkly.Sdk.Json"/> for details.
    /// </remarks>
    [JsonConverter(typeof(FeatureFlagsStateConverter))]
    public class FeatureFlagsState : IJsonSerializable
    {
        internal readonly bool _valid;
        internal readonly IDictionary<string, FlagState> _flags;
        private volatile ImmutableDictionary<string, LdValue> _immutableValuesMap; // lazily created
        
        /// <summary>
        /// True if this object contains a valid snapshot of feature flag state, or false if the
        /// state could not be computed (for instance, because the client was offline or there was no user).
        /// </summary>
        public bool Valid => _valid;

        /// <summary>
        /// Returns a builder for constructing a new instance of this class. May be useful in testing.
        /// </summary>
        /// <param name="options">the same options that can be passed to <see cref="ILdClient.AllFlagsState(Context, FlagsStateOption[])"/></param>
        /// <returns>a new <see cref="FeatureFlagsStateBuilder"/></returns>
        public static FeatureFlagsStateBuilder Builder(params FlagsStateOption[] options)
        {
            return new FeatureFlagsStateBuilder(options);
        }

        internal FeatureFlagsState(bool valid)
        {
            _valid = valid;
            _flags = new Dictionary<string, FlagState>();
        }

        internal FeatureFlagsState(bool valid, IDictionary<string, FlagState> flags)
        {
            _valid = valid;
            _flags = flags;
        }
        
        /// <summary>
        /// Returns the value of an individual feature flag at the time the state was recorded.
        /// </summary>
        /// <param name="key">the feature flag key</param>
        /// <returns>the flag's JSON value; <see cref="LdValue.Null"/> if the flag returned
        /// the default value, or if there was no such flag</returns>
        public LdValue GetFlagValueJson(string key) =>
            _flags.TryGetValue(key, out var flag) ? flag.Value : LdValue.Null;

        /// <summary>
        /// Returns the evaluation reason of an individual feature flag (as returned by
        /// <see cref="ILdClient.BoolVariation(string, Context, bool)"/>, etc.) at the time the state
        /// was recorded.
        /// </summary>
        /// <param name="key">the feature flag key</param>
        /// <returns>the evaluation reason; null if reasons were not recorded, or if there was no
        /// such flag</returns>
        public EvaluationReason? GetFlagReason(string key) =>
            _flags.TryGetValue(key, out var flag) ? flag.Reason : (EvaluationReason?)null;
        
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
                    _immutableValuesMap = _flags.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Value);
                }
                return _immutableValuesMap;
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object other) =>
            other is FeatureFlagsState o &&
                _valid == o._valid && DictionariesEqual(_flags, o._flags);

        /// <inheritdoc/>

        public override int GetHashCode() =>
            new HashCodeBuilder().With(_flags).With(_valid).Value;
        
        private static bool DictionariesEqual<T, U>(IDictionary<T, U> d0, IDictionary<T, U> d1) =>
            d0.Count == d1.Count && d0.All(kv =>
                d1.TryGetValue(kv.Key, out var v) && kv.Value.Equals(v));
    }

    /// <summary>
    /// A builder for constructing <see cref="FeatureFlagsState"/> instances.
    /// </summary>
    /// <remarks>
    /// This may be useful in test code. Use <see cref="FeatureFlagsState.Builder"/> to create a new builder.
    /// </remarks>
    public class FeatureFlagsStateBuilder
    {
        private readonly bool _detailsOnlyIfTracked;
        private readonly bool _withReasons;
        private bool _valid = true;
        private readonly Dictionary<string, FlagState> _flags= new Dictionary<string, FlagState>();

        internal FeatureFlagsStateBuilder(FlagsStateOption[] options)
        {
            _detailsOnlyIfTracked = FlagsStateOption.HasOption(options, FlagsStateOption.DetailsOnlyForTrackedFlags);
            _withReasons = FlagsStateOption.HasOption(options, FlagsStateOption.WithReasons);
        }

        /// <summary>
        /// Creates a <see cref="FeatureFlagsState"/> with the properties that have been set on the builder.
        /// </summary>
        /// <returns>a state object</returns>
        public FeatureFlagsState Build()
        {
            return new FeatureFlagsState(_valid, _flags);
        }

        /// <summary>
        /// Allows the state object to be marked as not valid (i.e. an error occurred, so flags could not be evaluated).
        /// </summary>
        /// <param name="valid">true if valid, false if invalid (default is valid)</param>
        /// <returns>the same builder</returns>
        public FeatureFlagsStateBuilder Valid(bool valid)
        {
            _valid = valid;
            return this;
        }

        /// <summary>
        /// Adds the result of a flag evaluation.
        /// </summary>
        /// <param name="flagKey">the flag key</param>
        /// <param name="result">the evaluation result</param>
        /// <returns></returns>
        public FeatureFlagsStateBuilder AddFlag(string flagKey, EvaluationDetail<LdValue> result)
        {
            return AddFlag(flagKey,
                result.Value,
                result.VariationIndex,
                result.Reason,
                0,
                false,
                false,
                null);
        }

        // This method is defined with internal scope because metadata fields like trackEvents aren't
        // relevant to the main external use case for the builder (testing server-side code)
        internal FeatureFlagsStateBuilder AddFlag(string flagKey, LdValue value, int? variationIndex, EvaluationReason reason,
            int flagVersion, bool flagTrackEvents, bool trackReason, UnixMillisecondTime? flagDebugEventsUntilDate)
        {
            bool flagIsTracked = flagTrackEvents || flagDebugEventsUntilDate != null;
            var flag = new FlagState
            {
                Value = value,
                Variation = variationIndex,
                Version = (!_detailsOnlyIfTracked || flagIsTracked) ? flagVersion : (int?)null,
                Reason = trackReason || (_withReasons && (!_detailsOnlyIfTracked || flagIsTracked)) ? reason : (EvaluationReason?)null,
                DebugEventsUntilDate = flagDebugEventsUntilDate,
                TrackEvents = flagTrackEvents,
                TrackReason = trackReason
            };
            _flags[flagKey] = flag;
            return this;
        }
    }

    internal struct FlagState
    {
        internal LdValue Value { get; set; }
        internal int? Variation { get; set; }
        internal int? Version { get; set; }
        internal bool TrackEvents { get; set; }
        internal bool TrackReason { get; set; }
        internal UnixMillisecondTime? DebugEventsUntilDate { get; set; }
        internal EvaluationReason? Reason { get; set; }

        public override bool Equals(object other)
        {
            if (other is FlagState o)
            {
                return Variation == o.Variation &&
                    Version == o.Version &&
                    TrackEvents == o.TrackEvents &&
                    TrackReason == o.TrackReason &&
                    DebugEventsUntilDate.Equals(o.DebugEventsUntilDate) &&
                    Object.Equals(Reason, o.Reason);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder().With(Variation).With(Version).With(TrackEvents).With(TrackReason).
                With(DebugEventsUntilDate).With(Reason).Value;
        }
    }

    internal class FeatureFlagsStateConverter : JsonConverter<FeatureFlagsState>
    {
        public override void Write(Utf8JsonWriter w, FeatureFlagsState state, JsonSerializerOptions options)
        {
            if (state is null)
            {
                w.WriteNullValue();
                return;
            }

            w.WriteStartObject();

            foreach (var entry in state._flags)
            {
                w.WritePropertyName(entry.Key);
                LdValueConverter.WriteJsonValue(entry.Value.Value, w);
            }

            w.WriteBoolean("$valid", state._valid);

            w.WriteStartObject("$flagsState");
            foreach (var entry in state._flags)
            {
                w.WriteStartObject(entry.Key);
                var meta = entry.Value;

                WriteIntIfNotNull(w, "variation", meta.Variation);
                WriteIntIfNotNull(w, "version", meta.Version);
                WriteBooleanIfTrue(w, "trackEvents", meta.TrackEvents);
                WriteBooleanIfTrue(w, "trackReason", meta.TrackReason);
                if (meta.DebugEventsUntilDate.HasValue)
                {
                    w.WriteNumber("debugEventsUntilDate", meta.DebugEventsUntilDate.Value.Value);
                }
                if (meta.Reason.HasValue)
                {
                    w.WritePropertyName("reason");
                    EvaluationReasonConverter.WriteJsonValue(meta.Reason.Value, w);
                }
                w.WriteEndObject();
            }
            w.WriteEndObject();

            w.WriteEndObject();
        }

        public override FeatureFlagsState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var valid = true;
            var flags = new Dictionary<string, FlagState>();
            for (var topLevelObj = RequireObject(ref reader); topLevelObj.Next(ref reader);)
            {
                var key = topLevelObj.Name;
                switch (key)
                {
                    case "$valid":
                        valid = reader.GetBoolean();
                        break;

                    case "$flagsState":
                        for (var flagsObj = RequireObject(ref reader); flagsObj.Next(ref reader);)
                        {
                            var subKey = flagsObj.Name;
                            var flag = flags.ContainsKey(subKey) ? flags[subKey] : new FlagState();
                            for (var metaObj = RequireObject(ref reader); metaObj.Next(ref reader);)
                            {
                                switch (metaObj.Name)
                                {
                                    case "variation":
                                        flag.Variation = GetIntOrNull(ref reader);
                                        break;
                                    case "version":
                                        flag.Version = GetIntOrNull(ref reader);
                                        break;
                                    case "trackEvents":
                                        flag.TrackEvents = reader.GetBoolean();
                                        break;
                                    case "debugEventsUntilDate":
                                        var n = GetLongOrNull(ref reader);
                                        flag.DebugEventsUntilDate = n.HasValue ? UnixMillisecondTime.OfMillis(n.Value) :
                                            (UnixMillisecondTime?)null;
                                        break;
                                    case "reason":
                                        flag.Reason = reader.TokenType == JsonTokenType.Null ? (EvaluationReason?)null :
                                                EvaluationReasonConverter.ReadJsonValue(ref reader);
                                        break;
                                }
                            }
                            flags[subKey] = flag;
                        }
                        break;

                    default:
                        var flagForValue = flags.ContainsKey(key) ? flags[key] : new FlagState();
                        flagForValue.Value = LdValueConverter.ReadJsonValue(ref reader);
                        flags[key] = flagForValue;
                        break;
                }
            }
            return new FeatureFlagsState(valid, flags);
        }
    }
}

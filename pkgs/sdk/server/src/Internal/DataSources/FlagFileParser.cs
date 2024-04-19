﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LaunchDarkly.Sdk.Server.Internal.Model;

using static LaunchDarkly.Sdk.Internal.JsonConverterHelpers;
using static LaunchDarkly.Sdk.Json.LdJsonConverters;
using static LaunchDarkly.Sdk.Server.Subsystems.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal sealed class FlagFileParser
    {
        private readonly Func<string, object> _alternateParser;

        public FlagFileParser(Func<string, object> alternateParser)
        {
            _alternateParser = alternateParser;
        }

        public FullDataSet<ItemDescriptor> Parse(string content, int version)
        {
            if (_alternateParser == null)
            {
                return ParseJson(content, version);
            }
            else
            {
                if (content.Trim().StartsWith("{"))
                {
                    try
                    {
                        return ParseJson(content, version);
                    }
                    catch (Exception)
                    {
                        // we failed to parse it as JSON, so we'll just see if the alternate parser can do it
                    }
                }
                // The alternate parser should produce the most basic .NET data structure that can represent
                // the file content, using types like Dictionary and String. We then convert this into a
                // JSON tree so we can use the JSON deserializer; this is inefficient, but it lets us reuse
                // our existing data model deserialization logic.
                var o = _alternateParser(content);
                var options = new JsonSerializerOptions();
                options.Converters.Add(new UntypedDictionaryJsonSerializer()); // see comments on this class below
                var asJson = JsonSerializer.Serialize(o, options);
                return ParseJson(asJson, version);
            }
        }

        private static FullDataSet<ItemDescriptor> ParseJson(string data, int version)
        {
            var r = new Utf8JsonReader(Encoding.UTF8.GetBytes(data));
            return ParseJson(ref r, version);
        }

        private static FullDataSet<ItemDescriptor> ParseJson(ref Utf8JsonReader r, int version)
        {
            var flagsBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, ItemDescriptor>>();
            var segmentsBuilder = ImmutableList.CreateBuilder<KeyValuePair<string, ItemDescriptor>>();
            for (var obj = RequireObject(ref r); obj.Next(ref r);)
            {
                switch (obj.Name)
                {
                    case "flags":
                        for (var subObj = RequireObjectOrNull(ref r); subObj.Next(ref r);)
                        {
                            var key = subObj.Name;
                            var flag = FeatureFlagSerialization.Instance.Read(ref r, null, null) as FeatureFlag;
                            flagsBuilder.Add(new KeyValuePair<string, ItemDescriptor>(key, new ItemDescriptor(version,
                                FlagWithVersion(flag, version))));
                        }
                        break;

                    case "flagValues":
                        for (var subObj = RequireObjectOrNull(ref r); subObj.Next(ref r);)
                        {
                            var key = subObj.Name;
                            var value = LdValueConverter.ReadJsonValue(ref r);
                            var flag = FlagWithValue(key, value, version);
                            flagsBuilder.Add(new KeyValuePair<string, ItemDescriptor>(key, new ItemDescriptor(version, flag)));
                        }
                        break;

                    case "segments":
                        for (var subObj = RequireObjectOrNull(ref r); subObj.Next(ref r);)
                        {
                            var key = subObj.Name;
                            var segment = SegmentSerialization.Instance.Read(ref r, null, null) as Segment;
                            segmentsBuilder.Add(new KeyValuePair<string, ItemDescriptor>(key, new ItemDescriptor(version,
                                SegmentWithVersion(segment, version))));
                        }
                        break;
                }
            }
            return new FullDataSet<ItemDescriptor>(ImmutableList.Create<KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>>(
                new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(DataModel.Features,
                    new KeyedItems<ItemDescriptor>(flagsBuilder.ToImmutable())),
                new KeyValuePair<DataKind, KeyedItems<ItemDescriptor>>(DataModel.Segments,
                    new KeyedItems<ItemDescriptor>(segmentsBuilder.ToImmutable()))
                ));
        }

        internal static FeatureFlag FlagWithVersion(FeatureFlag flag, int version) =>
            flag.Version == version ? flag :
            new FeatureFlag(
                flag.Key,
                version,
                flag.Deleted, flag.On, flag.Prerequisites, flag.Targets, flag.ContextTargets, flag.Rules, flag.Fallthrough,
                flag.OffVariation, flag.Variations, flag.Salt, flag.TrackEvents, flag.TrackEventsFallthrough,
                flag.DebugEventsUntilDate, flag.ClientSide, flag.SamplingRatio, flag.ExcludeFromSummaries, flag.Migration);

        // Constructs a flag that always returns the same value. This is done by giving it a
        // single variation and setting the fallthrough variation to that.
        internal static object FlagWithValue(string key, LdValue value, int version)
        {
            var json = LdValue.BuildObject()
                .Add("key", key)
                .Add("version", version)
                .Add("on", true)
                .Add("variations", LdValue.ArrayOf(value))
                .Add("fallthrough", LdValue.BuildObject().Add("variation", 0).Build())
                .Build()
                .ToJsonString();
            return DataModel.Features.Deserialize(json).Item;
        }

        internal static Segment SegmentWithVersion(Segment segment, int version) =>
            segment.Version == version ? segment :
            new Segment(
                segment.Key,
                version,
                segment.Deleted, segment.Included, segment.Excluded, segment.IncludedContexts, segment.ExcludedContexts,
                segment.Rules, segment.Salt, segment.Unbounded, segment.UnboundedContextKind, segment.Generation);

        // This custom JSON serializer addresses a problem that can happen when using an external YAML parser.
        // In JSON, the keys must always be strings, and System.Text.Json will refuse to either serialize or
        // deserialize anything with non-string keys. But in YAML, the keys can be of any type, so a YAML
        // parser that is told to deserialize some map-like data without a specific target type may decide to
        // return the type Dictionary<object, object> even if the keys really are strings.
        private class UntypedDictionaryJsonSerializer : JsonConverter<IDictionary<object, object>>
        {
            public override bool CanConvert(Type typeToConvert) =>
                typeof(IDictionary<object, object>).IsAssignableFrom(typeToConvert);

            public override IDictionary<object, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, IDictionary<object, object> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                foreach (var kv in value)
                {
                    writer.WritePropertyName(kv.Key.ToString());
                    JsonSerializer.Serialize(writer, kv.Value, options);
                }
                writer.WriteEndObject();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using LaunchDarkly.JsonStream;
using LaunchDarkly.Sdk.Json;
using LaunchDarkly.Sdk.Server.Internal.Model;

namespace LaunchDarkly.Sdk.Server.Internal.DataSources
{
    internal sealed class FlagFileParser
    {
        private readonly Func<string, object> _alternateParser;

        public FlagFileParser(Func<string, object> alternateParser)
        {
            _alternateParser = alternateParser;
        }
        
        public FlagFileData Parse(string content)
        {
            if (_alternateParser == null)
            {
                return ParseJson(content);
            }
            else
            {
                if (content.Trim().StartsWith("{"))
                {
                    try
                    {
                        return ParseJson(content);
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
                var r = JReader.FromAdapter(ReaderAdapters.FromSimpleTypes(o, allowTypeCoercion: true));
                return ParseJson(ref r);
            }
        }

        private static FlagFileData ParseJson(string data)
        {
            var r = JReader.FromString(data);
            return ParseJson(ref r);
        }

        private static FlagFileData ParseJson(ref JReader r)
        {
            var ret = new FlagFileData
            {
                Flags = new Dictionary<string, FeatureFlag>(),
                FlagValues = new Dictionary<string, LdValue>(),
                Segments = new Dictionary<string, Segment>()
            };
            for (var obj = r.Object(); obj.Next(ref r);)
            {
                switch (obj.Name.ToString())
                {
                    case "flags":
                        for (var subObj = r.ObjectOrNull(); subObj.Next(ref r);)
                        {
                            ret.Flags[subObj.Name.ToString()] = FeatureFlagSerialization.Instance.ReadJson(ref r) as FeatureFlag;
                        }
                        break;

                    case "flagValues":
                        for (var subObj = r.ObjectOrNull(); subObj.Next(ref r);)
                        {
                            ret.FlagValues[subObj.Name.ToString()] = (LdValue)new LdJsonConverters.LdValueConverter().ReadJson(ref r);
                        }
                        break;

                    case "segments":
                        for (var subObj = r.ObjectOrNull(); subObj.Next(ref r);)
                        {
                            ret.Segments[subObj.Name.ToString()] = SegmentSerialization.Instance.ReadJson(ref r) as Segment;
                        }
                        break;
                }
            }
            return ret;
        }
    }
}

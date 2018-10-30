using System;
using Newtonsoft.Json;

namespace LaunchDarkly.Client.Files
{
    internal class FlagFileParser
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
                return JsonConvert.DeserializeObject<FlagFileData>(content);
            }
            else
            {
                if (content.Trim().StartsWith("{"))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<FlagFileData>(content);
                    }
                    catch (Exception e)
                    {
                        // we failed to parse it as JSON, so we'll just see if the alternate parser can do it
                    }
                }
                // The alternate parser should produce the most basic .NET data structure that can represent
                // the file content, using types like Dictionary and String. We then convert this into a
                // JSON tree so we can use the JSON deserializer; this is inefficient, but we already know
                // that Gson can deserialize our model types correctly.
                var o = _alternateParser(content);
                var json = JsonConvert.SerializeObject(o);
                return JsonConvert.DeserializeObject<FlagFileData>(json);
            }
        }
    }
}

using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Sdk.Server.Internal
{
    public class JsonUtilTest
    {
        [Fact]
        public void DecodeJsonParsesSimpleValuesCorrectly()
        {
            Assert.Equal(new JValue(true), JsonUtil.DecodeJson<JToken>("true"));
            Assert.Equal(new JValue(1), JsonUtil.DecodeJson<JToken>("1"));
            Assert.Equal(new JValue(1.5f), JsonUtil.DecodeJson<JToken>("1.5"));
            Assert.Equal(new JValue("hello"), JsonUtil.DecodeJson<JToken>("\"hello\""));

            // ensure that a date-like string is *not* parsed as anything other than a string (ch49343)
            Assert.Equal(new JValue("1970-01-01T00:00:01.001Z"), JsonUtil.DecodeJson<JToken>("\"1970-01-01T00:00:01.001Z\""));
        }
    }
}

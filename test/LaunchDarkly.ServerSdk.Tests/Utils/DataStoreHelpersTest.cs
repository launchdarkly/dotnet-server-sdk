using LaunchDarkly.Client;
using LaunchDarkly.Client.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LaunchDarkly.Tests
{
    public class DataStoreHelpersTest
    {
        // Note that we already have tests in JsonSerializationTest that verify that our
        // Newtonsoft.Json serialization works for all the properties of FeatureFlag and
        // Segment, so the tests here just prove that the wrapper methods work as they should.

        [Fact]
        public void MarshalEntity()
        {
            var flag = new FeatureFlagBuilder("flag").Build();
            var actualStr = DataStoreHelpers.MarshalJson(flag);
            var actualJson = JsonConvert.DeserializeObject<JObject>(actualStr);
            Assert.Equal("flag", (string)actualJson.GetValue("key"));
        }

        [Fact]
        public void UnmarshalAsSpecificType()
        {
            var jsonStr = "{\"key\":\"flag\",\"on\":false}";
            var flag = DataStoreHelpers.UnmarshalJson(VersionedDataKind.Features, jsonStr);
            Assert.Equal("flag", flag.Key);
        }

        [Fact]
        public void UnmarshalAsGeneralType()
        {
            var jsonStr = "{\"key\":\"flag\",\"on\":false}";
            IVersionedDataKind kind = VersionedDataKind.Features;
            var flag = DataStoreHelpers.UnmarshalJson(kind, jsonStr);
            Assert.Equal("flag", flag.Key);
        }
    }
}
